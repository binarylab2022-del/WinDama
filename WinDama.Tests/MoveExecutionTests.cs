using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class MoveExecutionTests
{
    private readonly MoveGenerator _generator = new();

    [Test]
    public void ApplyMoveInPlace_QuietMove_MovesPieceAndClearsStart()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        var move = new Move((5, 1), (4, 0));

        MoveExecutionResult result = MoveExecutor.ApplyMoveInPlace(board, move, _generator);

        Assert.That(board[5, 1], Is.EqualTo(0));
        Assert.That(board[4, 0], Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(MoveExecutionResult.TurnShouldSwitch));
    }

    [Test]
    public void ApplyMoveInPlace_Capture_RemovesCapturedPiece()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        var move = new Move((5, 1), (3, 3), new() { (4, 2) });

        MoveExecutor.ApplyMoveInPlace(board, move, _generator);

        Assert.That(board[5, 1], Is.EqualTo(0));
        Assert.That(board[4, 2], Is.EqualTo(0));
        Assert.That(board[3, 3], Is.EqualTo(1));
    }

    [Test]
    public void ApplyMoveInPlace_Promotion_PlayerOneBecomesDamaOnTopRow()
    {
        int[,] board = TestBoards.Empty();
        board[1, 1] = 1;
        var move = new Move((1, 1), (0, 0));

        MoveExecutor.ApplyMoveInPlace(board, move, _generator);

        Assert.That(board[0, 0], Is.EqualTo(2));
    }

    [Test]
    public void ApplyMoveForSearch_DoesNotMutateOriginalBoard()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        var move = new Move((5, 1), (4, 0));

        int[,] searchedBoard = MoveExecutor.ApplyMoveForSearch(board, move, _generator);

        Assert.That(board[5, 1], Is.EqualTo(1));
        Assert.That(board[4, 0], Is.EqualTo(0));
        Assert.That(searchedBoard[5, 1], Is.EqualTo(0));
        Assert.That(searchedBoard[4, 0], Is.EqualTo(1));
    }

    [Test]
    public void ApplyMoveInPlace_MultiCapture_ResultAllowsTurnSwitchWhenSequenceComplete()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[2, 4] = -1;
        var move = new Move((5, 1), (1, 5), new() { (4, 2), (2, 4) });

        MoveExecutionResult result = MoveExecutor.ApplyMoveInPlace(board, move, _generator);

        Assert.That(board[1, 5], Is.EqualTo(1));
        Assert.That(board[4, 2], Is.EqualTo(0));
        Assert.That(board[2, 4], Is.EqualTo(0));
        Assert.That(result, Is.EqualTo(MoveExecutionResult.TurnShouldSwitch));
    }
    [Test]
    public void ApplyMoveInPlace_CaptureThatPromotes_DoesNotContinueAsNewDama()
    {
        int[,] board = TestBoards.Empty();
        board[2, 2] = 1;
        board[1, 3] = -1;

        // After promotion on (0,4), a new Dama would be able to capture this
        // piece, but promotion must end the current move sequence.
        board[1, 5] = -1;
        var move = new Move((2, 2), (0, 4), new() { (1, 3) });

        MoveExecutionResult result = MoveExecutor.ApplyMoveInPlace(board, move, _generator);

        Assert.That(board[0, 4], Is.EqualTo(2));
        Assert.That(result, Is.EqualTo(MoveExecutionResult.TurnShouldSwitch));
    }

}
