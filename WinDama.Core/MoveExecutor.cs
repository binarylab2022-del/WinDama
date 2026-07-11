using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Central source of truth for applying a move to a board.
///
/// This class deliberately contains no WPF/UI code: no MessageBox, no sounds,
/// no Canvas updates, and no game-over dialogs. It can therefore be used safely
/// by both the real game and the minimax simulation.
/// </summary>
public static class MoveExecutor
{
    public static MoveExecutionResult ApplyMoveInPlace(int[,] board, Move move, MoveGenerator moveGenerator, bool promote = true)
    {
        int piece = board[move.Start.Item1, move.Start.Item2];
        bool wasMan = piece == 1 || piece == -1;
        bool isCapture = move.CapturedPieces != null && move.CapturedPieces.Count > 0;
        bool reachesPromotionRow = wasMan && IsPromotionRow(piece, move.End.Item1);

        board[move.End.Item1, move.End.Item2] = piece;
        board[move.Start.Item1, move.Start.Item2] = 0;

        if (move.CapturedPieces != null)
        {
            foreach ((int row, int column) in move.CapturedPieces)
            {
                board[row, column] = 0;
            }
        }

        if (promote)
        {
            PromotePieceIfNeeded(board, move.End.Item1, move.End.Item2);
        }

        // A man that reaches the promotion row during a capture becomes a Dama
        // only after the move is complete. It must not immediately continue the
        // same capture sequence as a newly promoted Dama.
        if (isCapture && reachesPromotionRow)
        {
            return MoveExecutionResult.TurnShouldSwitch;
        }

        return HasFurtherCaptures(board, move, moveGenerator)
            ? MoveExecutionResult.SamePlayerMustContinueCapture
            : MoveExecutionResult.TurnShouldSwitch;
    }

    public static int[,] ApplyMoveForSearch(int[,] board, Move move, MoveGenerator moveGenerator, bool promote = true)
    {
        int[,] newBoard = (int[,])board.Clone();
        ApplyMoveInPlace(newBoard, move, moveGenerator, promote);
        return newBoard;
    }

    public static bool HasFurtherCaptures(int[,] board, Move move, MoveGenerator moveGenerator)
    {
        if (move.CapturedPieces == null || move.CapturedPieces.Count == 0)
        {
            return false;
        }

        int movedPiece = board[move.End.Item1, move.End.Item2];
        if (movedPiece == 0)
        {
            return false;
        }

        int player = movedPiece > 0 ? 1 : -1;
        List<Move> movesFromLandingSquare = moveGenerator.GetCapturesForPiece(
            board,
            player,
            move.End.Item1,
            move.End.Item2);

        return movesFromLandingSquare.Any(m => m.CapturedPieces != null && m.CapturedPieces.Count > 0);
    }

    private static bool IsPromotionRow(int piece, int row)
    {
        return (piece == 1 && row == 0) || (piece == -1 && row == 7);
    }

    public static void PromotePieceIfNeeded(int[,] board, int row, int column)
    {
        if (board[row, column] == 1 && row == 0)
        {
            board[row, column] = 2;
        }
        else if (board[row, column] == -1 && row == 7)
        {
            board[row, column] = -2;
        }
    }
}
