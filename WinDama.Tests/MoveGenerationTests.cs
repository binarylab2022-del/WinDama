using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class MoveGenerationTests
{
    private readonly MoveGenerator _generator = new();

    [Test]
    public void InitialPosition_PlayerOne_HasQuietMovesOnly()
    {
        int[,] board = TestBoards.InitialPosition();

        var moves = _generator.GetPlayerCapturesOrMoves(board, currentPlayer: 1);

        Assert.That(moves, Is.Not.Empty);
        Assert.That(moves.All(move => move.CapturedPieces.Count == 0), Is.True);
    }

    [Test]
    public void MandatoryCapture_WhenCaptureExists_ReturnsOnlyCaptures()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[5, 5] = 1; // this piece has a quiet move, but capture must be mandatory

        var moves = _generator.GetPlayerCapturesOrMoves(board, currentPlayer: 1);

        Assert.That(moves, Is.Not.Empty);
        Assert.That(moves.All(move => move.CapturedPieces.Count > 0), Is.True);
        Assert.That(moves.All(move => move.Start == (5, 1)), Is.True);
    }

    [Test]
    public void MaximumCaptureRule_ReturnsLongestCaptureSequencesOnly()
    {
        int[,] board = TestBoards.Empty();
        board[5, 3] = 1;
        board[4, 2] = -1;
        board[2, 2] = -1;
        board[4, 4] = -1;
        // From (5,3), player 1 has a short one-capture branch via (4,4)
        // and a longer two-capture branch via (4,2) then (2,2).
        // The shorter branch must be filtered out.

        var moves = _generator.GetPlayerCapturesOrMoves(board, currentPlayer: 1);

        Assert.That(moves, Is.Not.Empty);
        Assert.That(moves.All(move => move.CapturedPieces.Count == 2), Is.True);
    }

    [Test]
    public void DamaQuietMove_CanMoveAlongOpenDiagonal()
    {
        int[,] board = TestBoards.Empty();
        board[3, 3] = 2;

        var moves = _generator.GetSquareCapturesOrMoves(board, currentPlayer: 1, pieceRow: 3, pieceColumn: 3);

        Assert.That(moves.Any(move => move.Start == (3, 3) && move.End == (0, 0)), Is.True);
        Assert.That(moves.Any(move => move.Start == (3, 3) && move.End == (6, 6)), Is.True);
    }

    [Test]
    public void DamaCapture_CanLandBeyondCapturedPiece()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 2;
        board[3, 3] = -1;

        var moves = _generator.GetPlayerCapturesOrMoves(board, currentPlayer: 1);

        Assert.That(moves.Any(move => move.End == (2, 4) && move.CapturedPieces.Contains((3, 3))), Is.True);
        Assert.That(moves.Any(move => move.End == (1, 5) && move.CapturedPieces.Contains((3, 3))), Is.True);
    }

    [Test]
    public void SquareMoves_WhenAnotherPieceCanCapture_ReturnsNoQuietMovesForSelectedPiece()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1; // capture exists for piece at (5,1)
        board[5, 5] = 1;  // this piece has quiet moves only

        var selectedPieceMoves = _generator.GetSquareCapturesOrMoves(board, currentPlayer: 1, pieceRow: 5, pieceColumn: 5);

        Assert.That(selectedPieceMoves, Is.Empty);
    }

    [Test]
    public void SquareMoves_WhenSelectedPieceHasShorterCaptureThanGlobalMaximum_ReturnsNoMoves()
    {
        int[,] board = TestBoards.Empty();

        // Piece A has a two-capture sequence.
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[2, 4] = -1;

        // Piece B has only a one-capture sequence.
        board[5, 5] = 1;
        board[4, 6] = -1;

        var selectedShortCaptureMoves = _generator.GetSquareCapturesOrMoves(board, currentPlayer: 1, pieceRow: 5, pieceColumn: 5);
        var allLegalMoves = _generator.GetPlayerCapturesOrMoves(board, currentPlayer: 1);

        Assert.That(selectedShortCaptureMoves, Is.Empty);
        Assert.That(allLegalMoves, Is.Not.Empty);
        Assert.That(allLegalMoves.All(move => move.Start == (5, 1)), Is.True);
        Assert.That(allLegalMoves.All(move => move.CapturedPieces.Count == 2), Is.True);
    }

    [Test]
    public void PlayerMoves_WhenCaptureExists_DoesNotReturnAnyQuietMove()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[5, 5] = 1;

        var legalMoves = _generator.GetPlayerCapturesOrMoves(board, currentPlayer: 1);

        Assert.That(legalMoves, Is.Not.Empty);
        Assert.That(legalMoves.All(move => move.CapturedPieces.Count > 0), Is.True);
    }

    [Test]
    public void SquareMoves_DamaQuietMovesSuppressedWhenAnyCaptureExists()
    {
        int[,] board = TestBoards.Empty();
        board[2, 2] = 2;  // Dama has quiet moves but does not block the capture landing square.
        board[5, 1] = 1;  // This piece has a mandatory capture over (4,2) to (3,3).
        board[4, 2] = -1;

        var damaMoves = _generator.GetSquareCapturesOrMoves(board, currentPlayer: 1, pieceRow: 2, pieceColumn: 2);

        Assert.That(damaMoves, Is.Empty);
    }

    [Test]
    public void DamaShortCaptureSuppressedWhenAnotherPieceHasLongerCapture()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[2, 4] = -1;

        board[5, 5] = 2;
        board[4, 6] = -1;

        var damaShortCaptures = _generator.GetSquareCapturesOrMoves(board, currentPlayer: 1, pieceRow: 5, pieceColumn: 5);
        var legalMoves = _generator.GetPlayerCapturesOrMoves(board, currentPlayer: 1);

        Assert.That(damaShortCaptures, Is.Empty);
        Assert.That(legalMoves, Is.Not.Empty);
        Assert.That(legalMoves.All(move => move.CapturedPieces.Count == 2), Is.True);
        Assert.That(legalMoves.All(move => move.Start == (5, 1)), Is.True);
    }

}
