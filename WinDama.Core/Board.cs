namespace WinDama.Core;

public class Board
{
    public int[,] BoardState { get; private set; }

    public Board(int[,] initialState)
    {
        BoardState = (int[,])initialState.Clone();
    }

    public void ApplyMove(Move move)
    {
        MoveExecutor.ApplyMoveInPlace(BoardState, move, new MoveGenerator());
    }

    public Board Clone()
    {
        return new Board((int[,])BoardState.Clone());
    }
}
