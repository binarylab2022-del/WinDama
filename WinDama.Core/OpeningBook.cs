using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WinDama.Core;

/// <summary>
/// Opening book built from saved game records. It stores statistics by exact
/// board position and recommends only currently legal moves.
/// </summary>
public sealed class OpeningBook
{
    private readonly Dictionary<string, List<OpeningBookEntry>> entriesByPosition;
    private readonly Random random;

    public OpeningBook(IEnumerable<OpeningBookEntry> entries, Random? random = null)
    {
        Entries = entries?.ToList() ?? new List<OpeningBookEntry>();
        entriesByPosition = Entries
            .GroupBy(entry => entry.PositionKey)
            .ToDictionary(group => group.Key, group => group.ToList());
        this.random = random ?? new Random(0);
    }

    public IReadOnlyList<OpeningBookEntry> Entries { get; }
    public int EntryCount => Entries.Count;
    public int PositionCount => entriesByPosition.Count;

    public static OpeningBook Build(GameDatabase database, int maxBookPlies = 16)
    {
        if (database == null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        maxBookPlies = Math.Max(1, maxBookPlies);
        Dictionary<string, OpeningBookEntry> aggregate = new Dictionary<string, OpeningBookEntry>();
        MoveGenerator moveGenerator = new MoveGenerator();

        foreach (GameRecord game in database.Games)
        {
            int[,] board = game.CloneInitialBoard();
            int player = game.InitialPlayer == -1 ? -1 : 1;
            int max = Math.Min(maxBookPlies, game.Moves.Count);

            for (int index = 0; index < max; index++)
            {
                GameMoveRecord moveRecord = game.Moves[index];
                Move move = moveRecord.ToMove();
                List<Move> legalMoves = moveGenerator.GetPlayerCapturesOrMoves(board, player);
                Move? legalMove = legalMoves.FirstOrDefault(candidate => SameMove(candidate, move));
                if (legalMove == null)
                {
                    break;
                }

                string positionKey = ComputePositionKey(board, player);
                string moveKey = ComputeMoveKey(legalMove);
                string aggregateKey = positionKey + "|" + moveKey;
                if (!aggregate.TryGetValue(aggregateKey, out OpeningBookEntry? entry))
                {
                    entry = new OpeningBookEntry
                    {
                        PositionKey = positionKey,
                        Ply = index + 1,
                        MoveKey = moveKey,
                        MoveNotation = MoveNotation.Format(legalMove),
                        Player = player
                    };
                    aggregate[aggregateKey] = entry;
                }

                entry.Games++;
                double points = PointsForPlayer(game.Winner, player);
                entry.Points += points;
                if (points >= 0.99)
                {
                    entry.Wins++;
                }
                else if (points <= 0.01)
                {
                    entry.Losses++;
                }
                else
                {
                    entry.Draws++;
                }

                MoveExecutor.ApplyMoveInPlace(board, legalMove, moveGenerator);
                player = -player;
            }
        }

        return new OpeningBook(aggregate.Values
            .OrderBy(entry => entry.Ply)
            .ThenBy(entry => entry.PositionKey)
            .ThenByDescending(entry => entry.Games)
            .ThenByDescending(entry => entry.ScorePercent));
    }

    public OpeningBookRecommendation? RecommendMove(
        int[,] board,
        int player,
        IEnumerable<Move> legalMoves,
        OpeningBookMode mode = OpeningBookMode.BestScore,
        int minimumGames = 1)
    {
        string key = ComputePositionKey(board, player);
        if (!entriesByPosition.TryGetValue(key, out List<OpeningBookEntry>? entries))
        {
            return null;
        }

        Dictionary<string, Move> legalByKey = legalMoves.ToDictionary(ComputeMoveKey, move => move);
        List<OpeningBookEntry> usable = entries
            .Where(entry => entry.Games >= Math.Max(1, minimumGames) && legalByKey.ContainsKey(entry.MoveKey))
            .ToList();

        if (usable.Count == 0)
        {
            return null;
        }

        OpeningBookEntry selected = mode switch
        {
            OpeningBookMode.MostPlayed => usable
                .OrderByDescending(entry => entry.Games)
                .ThenByDescending(entry => entry.ScorePercent)
                .ThenBy(entry => entry.MoveNotation)
                .First(),
            OpeningBookMode.Exploration => SelectExplorationMove(usable),
            _ => usable
                .OrderByDescending(entry => entry.ScorePercent)
                .ThenByDescending(entry => entry.Games)
                .ThenBy(entry => entry.MoveNotation)
                .First()
        };

        return new OpeningBookRecommendation
        {
            Move = legalByKey[selected.MoveKey],
            Entry = selected,
            Reason = $"Book {mode}: {selected.MoveNotation}, {selected.Games} game(s), {selected.ScorePercent:0.0}%"
        };
    }

    public void SaveCsv(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("ply,player,move,games,wins,draws,losses,points,score_percent,position_key,move_key");
        foreach (OpeningBookEntry entry in Entries)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                entry.Ply.ToString(CultureInfo.InvariantCulture),
                entry.Player.ToString(CultureInfo.InvariantCulture),
                Csv(entry.MoveNotation),
                entry.Games.ToString(CultureInfo.InvariantCulture),
                entry.Wins.ToString(CultureInfo.InvariantCulture),
                entry.Draws.ToString(CultureInfo.InvariantCulture),
                entry.Losses.ToString(CultureInfo.InvariantCulture),
                entry.Points.ToString("0.0", CultureInfo.InvariantCulture),
                entry.ScorePercent.ToString("0.0", CultureInfo.InvariantCulture),
                Csv(entry.PositionKey),
                Csv(entry.MoveKey)
            }));
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    public static string ComputePositionKey(int[,] board, int player)
    {
        StringBuilder builder = new StringBuilder(140);
        builder.Append(player == -1 ? "b:" : "w:");
        for (int row = 0; row < board.GetLength(0); row++)
        {
            for (int column = 0; column < board.GetLength(1); column++)
            {
                builder.Append(board[row, column].ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
            }
        }

        return builder.ToString();
    }

    public static string ComputeMoveKey(Move move)
    {
        string captures = string.Join(";", move.CapturedPieces.Select(square => $"{square.Item1}:{square.Item2}"));
        return $"{move.Start.Item1}:{move.Start.Item2}>{move.End.Item1}:{move.End.Item2}|{captures}";
    }

    private OpeningBookEntry SelectExplorationMove(List<OpeningBookEntry> entries)
    {
        List<OpeningBookEntry> good = entries
            .Where(entry => entry.ScorePercent >= 40.0)
            .OrderByDescending(entry => entry.ScorePercent)
            .ThenByDescending(entry => entry.Games)
            .Take(4)
            .ToList();

        if (good.Count == 0)
        {
            good = entries;
        }

        return good[random.Next(good.Count)];
    }

    private static double PointsForPlayer(int winner, int player)
    {
        if (winner == 0)
        {
            return 0.5;
        }

        return winner == player ? 1.0 : 0.0;
    }

    private static bool SameMove(Move left, Move right)
    {
        return left.Start.Equals(right.Start)
            && left.End.Equals(right.End)
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value;
    }
}
