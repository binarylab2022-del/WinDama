using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Immutable, framework-neutral view of the current game state.
/// Use this when UI code needs to read the state without being allowed to
/// mutate controller internals directly.
/// </summary>
public sealed class GameState
{
    public GameState(
        int[,] board,
        int currentPlayer,
        GameMode gameMode,
        GameStatus status,
        Move? lastMove,
        IEnumerable<Move>? moveHistory = null)
    {
        Board = (int[,])board.Clone();
        CurrentPlayer = currentPlayer;
        GameMode = gameMode;
        Status = status;
        LastMove = CloneMove(lastMove);
        MoveHistory = (moveHistory ?? Enumerable.Empty<Move>())
            .Select(CloneMove)
            .Where(move => move != null)
            .Cast<Move>()
            .ToList();
    }

    public int[,] Board { get; }
    public int CurrentPlayer { get; }
    public GameMode GameMode { get; }
    public GameStatus Status { get; }
    public bool IsGameOver => Status != GameStatus.Ongoing;
    public Move? LastMove { get; }
    public IReadOnlyList<Move> MoveHistory { get; }

    public int[,] CloneBoard() => (int[,])Board.Clone();

    private static Move? CloneMove(Move? move)
    {
        return move == null
            ? null
            : new Move(move.Start, move.End, new List<(int, int)>(move.CapturedPieces));
    }
}
