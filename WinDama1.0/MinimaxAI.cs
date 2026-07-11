using System;
using WinDama.Core;

namespace WinDama;

/// <summary>
/// Compatibility wrapper kept for older UI/event code. The actual search now lives
/// in WinDama.Core.SearchEngine, so the WPF project no longer contains a second
/// minimax implementation.
/// </summary>
public sealed class MinimaxAI
{
    private readonly Board board;
    private readonly SearchEngine searchEngine;
    private readonly int depthLimit;

    public MinimaxAI(Board board, MoveGenerator moveGenerator, Evaluation evaluation, int depthLimit)
    {
        this.board = board ?? throw new ArgumentNullException(nameof(board));
        searchEngine = new SearchEngine(moveGenerator, evaluation);
        this.depthLimit = Math.Max(1, depthLimit);
    }

    public Move? GetBestMove(int currentPlayer)
    {
        SearchResult result = searchEngine.FindBestMove(
            board.BoardState,
            currentPlayer,
            SearchOptions.FixedDepth(depthLimit));

        return result.BestMove;
    }

    public int EvaluateBoard(Board board, int currentPlayer)
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        return new Evaluation().EvaluateBoard(board.BoardState, currentPlayer);
    }
}
