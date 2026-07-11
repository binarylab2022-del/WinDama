using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinDama.Core;

/// <summary>
/// Imports legacy WinDamas text opening files (*.ouv). The legacy files are
/// plain text lines containing 1-32 square-number moves such as "9-13".
/// Each non-empty line is treated as one opening/game line. Moves are validated
/// against the current engine rules, so illegal or unsupported tails are
/// ignored instead of corrupting the book.
/// </summary>
public static class OuvOpeningBookImporter
{
    private static readonly Regex MoveRegex = new Regex(@"(?<from>\d{1,2})\s*-\s*(?<to>\d{1,2})", RegexOptions.Compiled);

    public static GameDatabase ImportFiles(IEnumerable<string> filePaths, int maxPliesPerLine = 120, bool includeMirroredLines = false)
    {
        if (filePaths == null)
        {
            throw new ArgumentNullException(nameof(filePaths));
        }

        GameDatabase database = new GameDatabase { Name = "Imported .ouv opening database" };
        foreach (string filePath in filePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            GameDatabase imported = ImportFile(filePath, maxPliesPerLine, includeMirroredLines);
            foreach (GameRecord game in imported.Games)
            {
                database.Add(game);
            }
        }

        return database;
    }

    public static GameDatabase ImportFile(string filePath, int maxPliesPerLine = 120, bool includeMirroredLines = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A valid .ouv file path is required.", nameof(filePath));
        }

        string text = File.ReadAllText(filePath);
        return ImportText(text, Path.GetFileName(filePath), maxPliesPerLine, includeMirroredLines);
    }

    public static GameDatabase ImportText(string text, string sourceName = "imported.ouv", int maxPliesPerLine = 120, bool includeMirroredLines = false)
    {
        MoveGenerator moveGenerator = new MoveGenerator();
        GameDatabase database = new GameDatabase { Name = $"Imported .ouv: {sourceName}" };
        string[] lines = (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        int lineNumber = 0;
        foreach (string rawLine in lines)
        {
            lineNumber++;
            List<(int From, int To)> tokens = ParseMoveNumbers(rawLine).ToList();
            if (tokens.Count == 0)
            {
                continue;
            }

            GameRecord? game = ImportLine(tokens, sourceName, lineNumber, moveGenerator, Math.Max(1, maxPliesPerLine));
            if (game != null && game.Moves.Count > 0)
            {
                database.Add(game);
                if (includeMirroredLines)
                {
                    database.Add(CreateMirroredGame(game));
                }
            }
        }

        return database;
    }

    public static (int Row, int Column) SquareNumberToCoordinate(int squareNumber)
    {
        if (squareNumber < 1 || squareNumber > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(squareNumber), "Legacy square numbers must be between 1 and 32.");
        }

        int zeroBased = squareNumber - 1;
        int row = zeroBased / 4;
        int indexInRow = zeroBased % 4;
        int column = row % 2 == 0 ? indexInRow * 2 : indexInRow * 2 + 1;
        return (row, column);
    }

    public static int CoordinateToSquareNumber((int Row, int Column) coordinate)
    {
        int row = coordinate.Row;
        int column = coordinate.Column;
        if (row < 0 || row > 7 || column < 0 || column > 7 || (row + column) % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Coordinate is not a playable square.");
        }

        int indexInRow = row % 2 == 0 ? column / 2 : (column - 1) / 2;
        return row * 4 + indexInRow + 1;
    }

    private static GameRecord? ImportLine(
        IReadOnlyList<(int From, int To)> tokens,
        string sourceName,
        int lineNumber,
        MoveGenerator moveGenerator,
        int maxPliesPerLine)
    {
        int[,] board = GameController.CreateInitialBoard();
        int initialPlayer = InferInitialPlayer(board, tokens[0], moveGenerator);
        int player = initialPlayer;
        List<GameMoveRecord> records = new List<GameMoveRecord>();

        int max = Math.Min(tokens.Count, maxPliesPerLine);
        for (int index = 0; index < max; index++)
        {
            (int from, int to) = tokens[index];
            if (!TryFindLegalMove(board, player, from, to, moveGenerator, out Move? legalMove) || legalMove == null)
            {
                break;
            }

            records.Add(GameMoveRecord.FromMove(index + 1, player, legalMove));
            MoveExecutionResult execution = MoveExecutor.ApplyMoveInPlace(board, legalMove, moveGenerator);
            if (execution == MoveExecutionResult.TurnShouldSwitch)
            {
                player = -player;
            }
        }

        if (records.Count == 0)
        {
            return null;
        }

        return new GameRecord
        {
            EventName = $"Imported .ouv {sourceName}:{lineNumber}",
            PlayerOneProfile = "Imported .ouv Player 1",
            PlayerTwoProfile = "Imported .ouv Player 2",
            Winner = 0,
            WinnerProfile = "Unknown/Draw",
            ResultText = "1/2-1/2",
            StopReason = "Imported opening line",
            InitialPlayer = initialPlayer,
            InitialBoard = GameRecord.BoardToRows(GameController.CreateInitialBoard()),
            Moves = records
        };
    }

    /// <summary>
    /// Creates a color-swapped 180-degree rotated copy of an imported line.
    /// Legacy WinDamas books usually start with the top-side/black player.
    /// The current application can also start from Player 1 at the bottom, so
    /// this mirrored record makes the same opening knowledge usable for either
    /// side without changing the engine's normal initial position.
    /// </summary>
    private static GameRecord CreateMirroredGame(GameRecord source)
    {
        List<GameMoveRecord> mirroredMoves = source.Moves
            .Select(move => MirrorMoveRecord(move))
            .ToList();

        return new GameRecord
        {
            EventName = source.EventName + " [mirrored]",
            PlayerOneProfile = source.PlayerOneProfile + " mirrored",
            PlayerTwoProfile = source.PlayerTwoProfile + " mirrored",
            Winner = source.Winner == 0 ? 0 : -source.Winner,
            WinnerProfile = source.Winner == 0 ? source.WinnerProfile : source.WinnerProfile + " mirrored",
            ResultText = source.ResultText,
            StopReason = source.StopReason + "; mirrored for opposite starting side",
            InitialPlayer = -source.InitialPlayer,
            InitialBoard = GameRecord.BoardToRows(GameController.CreateInitialBoard()),
            Moves = mirroredMoves
        };
    }

    private static GameMoveRecord MirrorMoveRecord(GameMoveRecord record)
    {
        Move mirrored = new Move(
            MirrorSquare(record.Start.ToTuple()),
            MirrorSquare(record.End.ToTuple()),
            record.CapturedSquares.Select(square => MirrorSquare(square.ToTuple())).ToList());

        return GameMoveRecord.FromMove(record.Ply, -record.Player, mirrored, record.Evaluation, record.Depth);
    }

    private static (int Row, int Column) MirrorSquare((int Row, int Column) square)
    {
        return (7 - square.Row, 7 - square.Column);
    }

    private static IEnumerable<(int From, int To)> ParseMoveNumbers(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            yield break;
        }

        foreach (Match match in MoveRegex.Matches(line))
        {
            if (int.TryParse(match.Groups["from"].Value, out int from)
                && int.TryParse(match.Groups["to"].Value, out int to)
                && from >= 1 && from <= 32
                && to >= 1 && to <= 32)
            {
                yield return (from, to);
            }
        }
    }

    private static int InferInitialPlayer(int[,] board, (int From, int To) firstMove, MoveGenerator moveGenerator)
    {
        bool playerOneCanMove = TryFindLegalMove(board, 1, firstMove.From, firstMove.To, moveGenerator, out _);
        bool playerTwoCanMove = TryFindLegalMove(board, -1, firstMove.From, firstMove.To, moveGenerator, out _);

        if (playerTwoCanMove && !playerOneCanMove)
        {
            return -1;
        }

        return 1;
    }

    private static bool TryFindLegalMove(
        int[,] board,
        int player,
        int fromSquare,
        int toSquare,
        MoveGenerator moveGenerator,
        out Move? legalMove)
    {
        (int Row, int Column) start = SquareNumberToCoordinate(fromSquare);
        (int Row, int Column) end = SquareNumberToCoordinate(toSquare);
        legalMove = moveGenerator
            .GetPlayerCapturesOrMoves(board, player)
            .Where(move => move.Start.Equals(start) && move.End.Equals(end))
            .OrderByDescending(move => move.CapturedPieces.Count)
            .FirstOrDefault();

        return legalMove != null;
    }
}
