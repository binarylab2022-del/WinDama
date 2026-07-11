using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Serializable representation of one played move with optional search metadata.
/// </summary>
public sealed class GameMoveRecord
{
    public int Ply { get; init; }
    public int Player { get; init; }
    public SquareRecord Start { get; init; } = new SquareRecord();
    public SquareRecord End { get; init; } = new SquareRecord();
    public List<SquareRecord> CapturedSquares { get; init; } = new List<SquareRecord>();
    public int CapturedCount { get; init; }
    public int Evaluation { get; init; }
    public int Depth { get; init; }
    public string Notation { get; init; } = string.Empty;

    public Move ToMove()
    {
        return new Move(
            Start.ToTuple(),
            End.ToTuple(),
            CapturedSquares.Select(square => square.ToTuple()).ToList());
    }

    public static GameMoveRecord FromMove(int ply, int player, Move move, int evaluation = 0, int depth = 0)
    {
        return new GameMoveRecord
        {
            Ply = ply,
            Player = player,
            Start = SquareRecord.FromTuple(move.Start),
            End = SquareRecord.FromTuple(move.End),
            CapturedSquares = move.CapturedPieces.Select(SquareRecord.FromTuple).ToList(),
            CapturedCount = move.CapturedPieces.Count,
            Evaluation = evaluation,
            Depth = depth,
            Notation = MoveNotation.Format(move)
        };
    }
}
