namespace WinDama.Core;

/// <summary>
/// Benchmark outcome for one generated evaluation-weight candidate.
/// </summary>
public sealed class EvaluationWeightTuningResult
{
    public string CandidateName { get; init; } = string.Empty;
    public string ChangedWeightName { get; init; } = string.Empty;
    public int? OriginalValue { get; init; }
    public int? TunedValue { get; init; }
    public EvaluationWeights Weights { get; init; } = EvaluationWeights.Default;
    public TacticalBenchmarkSummary BenchmarkSummary { get; init; } = new TacticalBenchmarkSummary();

    public TacticalBenchmarkProfileSummary? ProfileSummary => BenchmarkSummary.Profiles.Count == 0
        ? null
        : BenchmarkSummary.Profiles[0];

    public int Score => ProfileSummary?.Score ?? 0;
    public int MaximumScore => ProfileSummary?.MaximumScore ?? 0;
    public double ScorePercent => ProfileSummary?.ScorePercent ?? 0;
    public int PerfectPositions => ProfileSummary?.PerfectPositions ?? 0;
    public int ScoredPositions => ProfileSummary?.ScoredPositions ?? 0;
    public double AverageNodes => ProfileSummary?.AverageNodes ?? 0;
}
