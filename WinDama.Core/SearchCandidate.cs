namespace WinDama.Core;

/// <summary>
/// One root candidate move and its evaluation score. Used by UI/analysis tools
/// to explain why the engine selected a move.
/// </summary>
public sealed class SearchCandidate
{
    public SearchCandidate(Move move, int score, int rank)
    {
        Move = move;
        Score = score;
        Rank = rank;
    }

    public Move Move { get; }
    public int Score { get; }
    public int Rank { get; }
}
