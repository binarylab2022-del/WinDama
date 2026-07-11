using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Aggregated result of a tactical benchmark run.
/// </summary>
public sealed class TacticalBenchmarkSummary
{
    public IReadOnlyList<TacticalBenchmarkPositionResult> Results { get; init; } = new List<TacticalBenchmarkPositionResult>();

    public int PositionCount => Results.Select(r => r.PositionFile).Distinct().Count();
    public int ProfileCount => Results.Select(r => r.ProfileName).Distinct().Count();
    public int ResultCount => Results.Count;

    public IReadOnlyList<TacticalBenchmarkProfileSummary> Profiles => Results
        .GroupBy(r => r.ProfileName)
        .Select(g => new TacticalBenchmarkProfileSummary
        {
            ProfileName = g.Key,
            PositionCount = g.Count(),
            AverageEvaluation = g.Average(r => r.Evaluation),
            AverageNodes = g.Average(r => r.Nodes),
            AverageQuiescenceNodes = g.Average(r => r.QuiescenceNodes),
            AverageNodesPerSecond = g.Average(r => r.NodesPerSecond),
            CapturePositions = g.Count(r => r.CaptureMoveCount > 0),
            Score = g.Sum(r => r.Score),
            MaximumScore = g.Sum(r => r.MaximumScore),
            ScoredPositions = g.Count(r => r.HasExpectations),
            PerfectPositions = g.Count(r => r.HasExpectations && r.Score == r.MaximumScore)
        })
        .OrderByDescending(p => p.ScorePercent)
        .ThenByDescending(p => p.Score)
        .ThenByDescending(p => p.AverageEvaluation)
        .ToList();
}

public sealed class TacticalBenchmarkProfileSummary
{
    public string ProfileName { get; init; } = string.Empty;
    public int PositionCount { get; init; }
    public double AverageEvaluation { get; init; }
    public double AverageNodes { get; init; }
    public double AverageQuiescenceNodes { get; init; }
    public double AverageNodesPerSecond { get; init; }
    public int CapturePositions { get; init; }
    public int Score { get; init; }
    public int MaximumScore { get; init; }
    public int ScoredPositions { get; init; }
    public int PerfectPositions { get; init; }
    public double ScorePercent => MaximumScore <= 0 ? 0 : (double)Score * 100.0 / MaximumScore;
}

