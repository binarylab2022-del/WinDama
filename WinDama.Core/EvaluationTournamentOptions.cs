namespace WinDama.Core;

/// <summary>
/// Options for profile-vs-profile engine tournaments. The tournament is
/// deterministic by default so that profile changes can be compared reliably.
/// </summary>
public sealed class EvaluationTournamentOptions
{
    public int Depth { get; init; } = 3;
    public int ForcedCaptureComparisonDepth { get; init; } = 3;
    public bool UseQuiescenceSearch { get; init; } = true;
    public int MaxQuiescenceDepth { get; init; } = 8;
    public bool UseTranspositionTable { get; init; } = true;
    public bool UseKillerMoves { get; init; } = true;
    public bool UseHistoryHeuristic { get; init; } = true;
    public bool RandomizeNearBestMoves { get; init; } = false;
    public int GamesPerColor { get; init; } = 1;
    public int MaxPliesPerGame { get; init; } = 160;
    public int MaxMovesWithoutCaptureOrPromotion { get; init; } = 80;

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
