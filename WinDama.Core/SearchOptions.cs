using System;
using System.Threading;

namespace WinDama.Core;

/// <summary>
/// Immutable configuration for one AI search request.
/// It intentionally contains no WPF/UI objects.
/// </summary>
public sealed class SearchOptions
{
    public SearchMode Mode { get; init; } = SearchMode.FixedDepth;
    public int MaximumDepth { get; init; } = 5;
    public int? TimeLimitMilliseconds { get; init; }
    public bool Iterative { get; init; }
    public int TieBreakTolerance { get; init; } = 0;
    public bool RandomizeNearBestMoves { get; init; } = true;
    public Action<SearchProgress>? Progress { get; init; }
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
    public int ProgressIntervalMilliseconds { get; init; } = 250;
    public bool UseTranspositionTable { get; init; } = true;
    public int TranspositionTableMaximumEntries { get; init; } = 250_000;
    /// <summary>
    /// Depth used to compare several mandatory longest captures. This search is
    /// tactical and does not consume the game-clock budget.
    /// </summary>
    public int ForcedCaptureComparisonDepth { get; init; } = 5;

    /// <summary>
    /// Extends leaf nodes through mandatory capture sequences instead of
    /// evaluating tactically unstable positions directly. This reduces horizon
    /// blunders in capture-heavy checkers positions.
    /// </summary>
    public bool UseQuiescenceSearch { get; init; } = true;

    /// <summary>
    /// Maximum number of extra capture-only plies searched after the normal
    /// minimax depth reaches zero. A value between 6 and 10 is usually enough
    /// for this 8x8 engine while avoiding runaway positions.
    /// </summary>
    public int MaxQuiescenceDepth { get; init; } = 8;

    /// <summary>
    /// Enables killer-move ordering: non-capture moves that caused beta cutoffs
    /// at the same remaining depth are searched early in sibling nodes.
    /// </summary>
    public bool UseKillerMoves { get; init; } = true;

    /// <summary>
    /// Number of killer moves remembered for each remaining search depth.
    /// Two is the standard practical value for alpha-beta engines.
    /// </summary>
    public int KillerMovesPerDepth { get; init; } = 2;

    /// <summary>
    /// Enables the history heuristic: moves that frequently cause beta cutoffs
    /// receive a higher ordering score later in the same search.
    /// </summary>
    public bool UseHistoryHeuristic { get; init; } = true;

    public static SearchOptions FixedDepth(int depth, int tieBreakTolerance = 0)
    {
        return new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = depth < 1 ? 1 : depth,
            TimeLimitMilliseconds = null,
            Iterative = false,
            TieBreakTolerance = tieBreakTolerance
        };
    }
}
