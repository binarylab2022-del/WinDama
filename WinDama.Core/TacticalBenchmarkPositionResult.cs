using System;

namespace WinDama.Core;

/// <summary>
/// Result for one profile on one tactical position.
/// </summary>
public sealed class TacticalBenchmarkPositionResult
{
    public string PositionName { get; init; } = string.Empty;
    public string PositionFile { get; init; } = string.Empty;
    public string ProfileName { get; init; } = string.Empty;
    public string EvaluatorType { get; init; } = "handcrafted";
    public int SideToMove { get; init; }
    public int Evaluation { get; init; }
    public int CompletedDepth { get; init; }
    public int Nodes { get; init; }
    public int QuiescenceNodes { get; init; }
    public double NodesPerSecond { get; init; }
    public TimeSpan Elapsed { get; init; }
    public int LegalMoveCount { get; init; }
    public int CaptureMoveCount { get; init; }
    public string BestMove { get; init; } = string.Empty;
    public string PrincipalVariation { get; init; } = string.Empty;
    public string TopMoves { get; init; } = string.Empty;
    public string StopReason { get; init; } = string.Empty;
    public int Score { get; init; }
    public int MaximumScore { get; init; }
    public double ScorePercent => MaximumScore <= 0 ? 0 : (double)Score * 100.0 / MaximumScore;
    public bool HasExpectations => MaximumScore > 0;
    public bool BestMoveMatchesExpectation { get; init; }
    public bool EvaluationSignMatchesExpectation { get; init; }
    public bool LegalMoveCountMatchesExpectation { get; init; }
    public bool CaptureMoveCountMatchesExpectation { get; init; }
    public bool BestMoveCaptureCountMatchesExpectation { get; init; }
    public string ExpectedBestMove { get; init; } = string.Empty;
    public string ExpectedEvaluationSign { get; init; } = string.Empty;
    public int? ExpectedLegalMoveCount { get; init; }
    public int? ExpectedCaptureMoveCount { get; init; }
    public int? ExpectedMinBestMoveCaptureCount { get; init; }
    public string ScoreDetails { get; init; } = string.Empty;
    public long TranspositionHits { get; init; }
    public long TranspositionCutoffs { get; init; }
    public long KillerMoveCutoffs { get; init; }
    public long HistoryHeuristicUpdates { get; init; }
}
