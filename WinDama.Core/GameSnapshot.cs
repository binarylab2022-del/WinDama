using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Framework-neutral game-state snapshot. It can be used by UI, tests, save/load,
/// and undo/redo without referencing WPF controls.
/// </summary>
public sealed class GameSnapshot
{
    public int[,] Board { get; init; }
    public int CurrentPlayer { get; init; }
    public bool IsGameOver { get; init; }
    public Move? LastMove { get; init; }
    public IReadOnlyList<Move> MoveHistory { get; init; }

    public GameSnapshot(
        int[,] board,
        int currentPlayer,
        bool isGameOver = false,
        Move? lastMove = null,
        IEnumerable<Move>? moveHistory = null)
    {
        Board = (int[,])board.Clone();
        CurrentPlayer = currentPlayer;
        IsGameOver = isGameOver;
        LastMove = CloneMove(lastMove);
        MoveHistory = (moveHistory ?? Enumerable.Empty<Move>())
            .Select(CloneMove)
            .Where(move => move != null)
            .Cast<Move>()
            .ToList();
    }

    public int[,] CloneBoard()
    {
        return (int[,])Board.Clone();
    }

    private static Move? CloneMove(Move? move)
    {
        return move == null
            ? null
            : new Move(move.Start, move.End, new List<(int, int)>(move.CapturedPieces));
    }
}
