using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WinDama.Core;

/// <summary>
/// Complete tournament matrix and profile ranking.
/// </summary>
public sealed class EvaluationTournamentSummary
{
    public string ProfileA { get; init; } = string.Empty;
    public string ProfileB { get; init; } = string.Empty;
    public IReadOnlyList<EvaluationTournamentGameResult> Games { get; init; } = new List<EvaluationTournamentGameResult>();

    public int GameCount => Games.Count;

    public IReadOnlyList<EvaluationTournamentProfileSummary> Profiles
    {
        get
        {
            var names = Games
                .SelectMany(game => new[] { game.PlayerOneProfile, game.PlayerTwoProfile })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            return names
                .Select(name => BuildProfileSummary(name))
                .OrderByDescending(summary => summary.Points)
                .ThenByDescending(summary => summary.ScorePercent)
                .ThenBy(summary => summary.ProfileName)
                .ToList();
        }
    }

    public EvaluationTournamentProfileSummary? Winner => Profiles.FirstOrDefault();

    public void SaveCsv(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("game,player1,player2,result,winner,winner_profile,plies,moves,nodes,qnodes,elapsed_ms,tt_hits,tt_cutoffs,killer_cutoffs,history_updates,stop_reason,move_log");

        foreach (EvaluationTournamentGameResult game in Games)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                game.GameNumber.ToString(CultureInfo.InvariantCulture),
                Csv(game.PlayerOneProfile),
                Csv(game.PlayerTwoProfile),
                Csv(game.ResultText),
                game.Winner.ToString(CultureInfo.InvariantCulture),
                Csv(game.WinnerProfile),
                game.PlyCount.ToString(CultureInfo.InvariantCulture),
                game.MoveCount.ToString(CultureInfo.InvariantCulture),
                game.Nodes.ToString(CultureInfo.InvariantCulture),
                game.QuiescenceNodes.ToString(CultureInfo.InvariantCulture),
                game.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture),
                game.TranspositionHits.ToString(CultureInfo.InvariantCulture),
                game.TranspositionCutoffs.ToString(CultureInfo.InvariantCulture),
                game.KillerMoveCutoffs.ToString(CultureInfo.InvariantCulture),
                game.HistoryHeuristicUpdates.ToString(CultureInfo.InvariantCulture),
                Csv(game.StopReason),
                Csv(game.MoveLog)
            }));
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    public void SaveGameLog(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Tournament: {ProfileA} vs {ProfileB}");
        builder.AppendLine($"Games: {GameCount}");
        builder.AppendLine();

        foreach (EvaluationTournamentProfileSummary profile in Profiles)
        {
            builder.AppendLine($"{profile.ProfileName}: {profile.Points:0.0}/{profile.Games} ({profile.ScorePercent:0.0}%) W{profile.Wins} D{profile.Draws} L{profile.Losses}");
        }

        builder.AppendLine();

        foreach (EvaluationTournamentGameResult game in Games)
        {
            builder.AppendLine($"Game {game.GameNumber}: {game.PlayerOneProfile} vs {game.PlayerTwoProfile}  {game.ResultText}");
            builder.AppendLine($"Stop: {game.StopReason}; plies: {game.PlyCount}; nodes: {game.Nodes}; qnodes: {game.QuiescenceNodes}");
            builder.AppendLine(game.MoveLog);
            builder.AppendLine();
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    private EvaluationTournamentProfileSummary BuildProfileSummary(string profileName)
    {
        int games = 0;
        int wins = 0;
        int losses = 0;
        int draws = 0;
        double points = 0;
        long nodes = 0;
        long qnodes = 0;
        int plies = 0;

        foreach (EvaluationTournamentGameResult game in Games)
        {
            bool asPlayerOne = string.Equals(game.PlayerOneProfile, profileName, System.StringComparison.OrdinalIgnoreCase);
            bool asPlayerTwo = string.Equals(game.PlayerTwoProfile, profileName, System.StringComparison.OrdinalIgnoreCase);
            if (!asPlayerOne && !asPlayerTwo)
            {
                continue;
            }

            games++;
            nodes += game.Nodes;
            qnodes += game.QuiescenceNodes;
            plies += game.PlyCount;

            if (game.Winner == 0)
            {
                draws++;
                points += 0.5;
            }
            else if ((game.Winner == 1 && asPlayerOne) || (game.Winner == -1 && asPlayerTwo))
            {
                wins++;
                points += 1.0;
            }
            else
            {
                losses++;
            }
        }

        return new EvaluationTournamentProfileSummary
        {
            ProfileName = profileName,
            Games = games,
            Wins = wins,
            Losses = losses,
            Draws = draws,
            Points = points,
            AveragePlyCount = games <= 0 ? 0 : (double)plies / games,
            AverageNodes = games <= 0 ? 0 : (double)nodes / games,
            AverageQuiescenceNodes = games <= 0 ? 0 : (double)qnodes / games
        };
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
