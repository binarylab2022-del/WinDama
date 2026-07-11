namespace WinDama.Core;

/// <summary>
/// Aggregated tournament score for one evaluation profile.
/// </summary>
public sealed class EvaluationTournamentProfileSummary
{
    public string ProfileName { get; init; } = string.Empty;
    public int Games { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public int Draws { get; init; }
    public double Points { get; init; }
    public double ScorePercent => Games <= 0 ? 0 : Points * 100.0 / Games;
    public double AveragePlyCount { get; init; }
    public double AverageNodes { get; init; }
    public double AverageQuiescenceNodes { get; init; }
}
