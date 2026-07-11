using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WinDama.Core;

/// <summary>
/// Ranked output of an evaluation-weight tuning run.
/// </summary>
public sealed class EvaluationWeightTuningSummary
{
    public string BaseProfileName { get; init; } = string.Empty;
    public EvaluationWeights BaseWeights { get; init; } = EvaluationWeights.Default;
    public IReadOnlyList<EvaluationWeightTuningResult> Results { get; init; } = new List<EvaluationWeightTuningResult>();

    public int CandidateCount => Results.Count;
    public EvaluationWeightTuningResult? BestResult => Results
        .OrderByDescending(result => result.ScorePercent)
        .ThenByDescending(result => result.Score)
        .ThenByDescending(result => result.PerfectPositions)
        .ThenBy(result => result.AverageNodes)
        .FirstOrDefault();

    public IReadOnlyList<EvaluationWeightTuningResult> RankedResults => Results
        .OrderByDescending(result => result.ScorePercent)
        .ThenByDescending(result => result.Score)
        .ThenByDescending(result => result.PerfectPositions)
        .ThenBy(result => result.AverageNodes)
        .ToList();

    public void SaveBestWeights(string filePath)
    {
        if (BestResult == null)
        {
            throw new InvalidOperationException("No tuned candidates are available to export.");
        }

        BestResult.Weights.Save(filePath);
    }

    public static void SaveCsv(EvaluationWeightTuningSummary summary, string filePath)
    {
        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("rank,candidate,changed_weight,original_value,tuned_value,score,max_score,score_percent,perfect_positions,scored_positions,average_nodes");

        int rank = 1;
        foreach (EvaluationWeightTuningResult result in summary.RankedResults)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                rank.ToString(CultureInfo.InvariantCulture),
                Csv(result.CandidateName),
                Csv(result.ChangedWeightName),
                result.OriginalValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result.TunedValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result.Score.ToString(CultureInfo.InvariantCulture),
                result.MaximumScore.ToString(CultureInfo.InvariantCulture),
                result.ScorePercent.ToString("0.0", CultureInfo.InvariantCulture),
                result.PerfectPositions.ToString(CultureInfo.InvariantCulture),
                result.ScoredPositions.ToString(CultureInfo.InvariantCulture),
                result.AverageNodes.ToString("0.0", CultureInfo.InvariantCulture)
            }));
            rank++;
        }

        File.WriteAllText(filePath, builder.ToString());
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
