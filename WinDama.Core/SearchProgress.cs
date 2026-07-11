using System;
using System.Collections.Generic;

namespace WinDama.Core;

/// <summary>
/// Incremental, UI-independent search information. The WPF layer can display
/// these values while the core search is still running.
/// </summary>
public sealed class SearchProgress
{
    public int CurrentDepth { get; init; }
    public int CompletedDepth { get; init; }
    public int Nodes { get; init; }
    public int QuiescenceNodes { get; init; }
    public double NodesPerSecond { get; init; }
    public int BestEvaluation { get; init; }
    public Move? BestMove { get; init; }
    public Move? CurrentMove { get; init; }
    public TimeSpan Elapsed { get; init; }
    public int LegalMoveCount { get; init; }
    public int CaptureMoveCount { get; init; }
    public bool IsForcedCaptureFastPath { get; init; }
    public bool TimedOut { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<SearchCandidate> TopMoves { get; init; } = Array.Empty<SearchCandidate>();
    public IReadOnlyList<Move> PrincipalVariation { get; init; } = Array.Empty<Move>();
    public long TranspositionHits { get; init; }
    public long TranspositionStores { get; init; }
    public long TranspositionCutoffs { get; init; }
    public int TranspositionEntries { get; init; }
    public bool UsedQuiescenceSearch { get; init; }
    public int MaxQuiescenceDepth { get; init; }
    public long KillerMoveCutoffs { get; init; }
    public long HistoryHeuristicUpdates { get; init; }
}
