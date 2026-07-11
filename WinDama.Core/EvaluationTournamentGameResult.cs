using System;
using System.Collections.Generic;

namespace WinDama.Core;

/// <summary>
/// Result of one complete engine-vs-engine game between evaluation profiles.
/// </summary>
public sealed class EvaluationTournamentGameResult
{
    public int GameNumber { get; init; }
    public string PlayerOneProfile { get; init; } = string.Empty;
    public string PlayerTwoProfile { get; init; } = string.Empty;
    public string FirstRequestedProfile { get; init; } = string.Empty;
    public string SecondRequestedProfile { get; init; } = string.Empty;
    public int Winner { get; init; }
    public string WinnerProfile { get; init; } = "Draw";
    public string ResultText { get; init; } = "1/2-1/2";
    public string StopReason { get; init; } = string.Empty;
    public int PlyCount { get; init; }
    public int MoveCount => (PlyCount + 1) / 2;
    public long Nodes { get; init; }
    public long QuiescenceNodes { get; init; }
    public long TranspositionHits { get; init; }
    public long TranspositionCutoffs { get; init; }
    public long KillerMoveCutoffs { get; init; }
    public long HistoryHeuristicUpdates { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string MoveLog { get; init; } = string.Empty;
    public IReadOnlyList<GameMoveRecord> MoveRecords { get; init; } = new List<GameMoveRecord>();
    public int FinalPlayerToMove { get; init; }
}
