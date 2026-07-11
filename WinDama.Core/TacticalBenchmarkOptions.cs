namespace WinDama.Core;

/// <summary>
/// Configuration for a deterministic tactical benchmark run.
/// </summary>
public sealed class TacticalBenchmarkOptions
{
    public int Depth { get; init; } = 5;
    public int ForcedCaptureComparisonDepth { get; init; } = 5;
    public bool UseQuiescenceSearch { get; init; } = true;
    public int MaxQuiescenceDepth { get; init; } = 8;
    public bool UseTranspositionTable { get; init; } = true;
    public bool UseKillerMoves { get; init; } = true;
    public bool UseHistoryHeuristic { get; init; } = true;
    public bool RandomizeNearBestMoves { get; init; } = false;
    public int MaximumPositions { get; init; } = int.MaxValue;

    public SearchOptions ToSearchOptions()
    {
        return new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = Depth < 1 ? 1 : Depth,
            Iterative = false,
            TieBreakTolerance = 0,
            RandomizeNearBestMoves = RandomizeNearBestMoves,
            ForcedCaptureComparisonDepth = ForcedCaptureComparisonDepth < 1 ? 1 : ForcedCaptureComparisonDepth,
            UseQuiescenceSearch = UseQuiescenceSearch,
            MaxQuiescenceDepth = MaxQuiescenceDepth < 0 ? 0 : MaxQuiescenceDepth,
            UseTranspositionTable = UseTranspositionTable,
            UseKillerMoves = UseKillerMoves,
            UseHistoryHeuristic = UseHistoryHeuristic
        };
    }
}
