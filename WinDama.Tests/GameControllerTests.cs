using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class GameControllerTests
{
    [Test]
    public void NewController_InitialPosition_PlayerOneToMoveAndGameIsOngoing()
    {
        var controller = new GameController(TestBoards.InitialPosition());

        Assert.That(controller.CurrentPlayer, Is.EqualTo(1));
        Assert.That(controller.IsGameOver, Is.False);
        Assert.That(controller.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(controller.GetCurrentLegalMoves(), Is.Not.Empty);
    }

    [Test]
    public void ApplyMove_QuietMove_SwitchesTurn()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[2, 2] = -1;
        var controller = new GameController(board, currentPlayer: 1);

        GameTurnResult result = controller.ApplyMove(new Move((5, 1), (4, 0)));

        Assert.That(result.MoveApplied, Is.True);
        Assert.That(result.TurnSwitched, Is.True);
        Assert.That(controller.CurrentPlayer, Is.EqualTo(-1));
        Assert.That(controller.Board[5, 1], Is.EqualTo(0));
        Assert.That(controller.Board[4, 0], Is.EqualTo(1));
    }

    [Test]
    public void ApplyMove_MandatoryCapture_RemovesCapturedPieceAndSwitchesTurnWhenSequenceEnds()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[0, 6] = -1;
        var controller = new GameController(board, currentPlayer: 1);

        GameTurnResult result = controller.ApplyMove(new Move((5, 1), (3, 3), new() { (4, 2) }));

        Assert.That(result.MoveApplied, Is.True);
        Assert.That(controller.Board[4, 2], Is.EqualTo(0));
        Assert.That(controller.Board[3, 3], Is.EqualTo(1));
        Assert.That(controller.CurrentPlayer, Is.EqualTo(-1));
    }

    [Test]
    public void ApplyMove_IllegalMove_DoesNotMutateBoardOrSwitchTurn()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        var controller = new GameController(board, currentPlayer: 1);

        GameTurnResult result = controller.ApplyMove(new Move((5, 1), (5, 3)));

        Assert.That(result.MoveApplied, Is.False);
        Assert.That(controller.CurrentPlayer, Is.EqualTo(1));
        Assert.That(controller.Board[5, 1], Is.EqualTo(1));
        Assert.That(controller.Board[5, 3], Is.EqualTo(0));
    }

    [Test]
    public void ApplyMove_NoLegalMoveForNextPlayer_MarksGameOver()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        // Player 2 has no piece and therefore no legal move after Player 1 moves.
        var controller = new GameController(board, currentPlayer: 1);

        GameTurnResult result = controller.ApplyMove(new Move((5, 1), (4, 0)));

        Assert.That(result.MoveApplied, Is.True);
        Assert.That(controller.IsGameOver, Is.True);
        Assert.That(controller.Status, Is.EqualTo(GameStatus.PlayerOneWon));
    }

    [Test]
    public void GameMode_HumanVsAi_OnlyPlayerTwoIsAiControlled()
    {
        var controller = new GameController(TestBoards.InitialPosition(), gameMode: GameMode.HumanVsAI);

        Assert.That(controller.IsAiControlledPlayer(1), Is.False);
        Assert.That(controller.IsAiControlledPlayer(-1), Is.True);
    }

    [Test]
    public void GameMode_AiVsAi_BothSidesAreAiControlled()
    {
        var controller = new GameController(TestBoards.InitialPosition(), gameMode: GameMode.AIVsAI);

        Assert.That(controller.IsAiControlledPlayer(1), Is.True);
        Assert.That(controller.IsAiControlledPlayer(-1), Is.True);
    }

    [Test]
    public void Snapshot_Restore_ReturnsToPreviousBoardAndPlayer()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[2, 2] = -1;
        var controller = new GameController(board, currentPlayer: 1);
        GameSnapshot snapshot = controller.CreateSnapshot();

        controller.ApplyMove(new Move((5, 1), (4, 0)));
        controller.RestoreSnapshot(snapshot);

        Assert.That(controller.CurrentPlayer, Is.EqualTo(1));
        Assert.That(controller.Board[5, 1], Is.EqualTo(1));
        Assert.That(controller.Board[4, 0], Is.EqualTo(0));
    }

    [Test]
    public void State_ReturnsImmutableReadModel()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        var controller = new GameController(board, currentPlayer: 1, gameMode: GameMode.HumanVsHuman);

        GameState state = controller.State;
        state.Board[5, 1] = 0;

        Assert.That(controller.Board[5, 1], Is.EqualTo(1));
        Assert.That(state.CurrentPlayer, Is.EqualTo(1));
        Assert.That(state.GameMode, Is.EqualTo(GameMode.HumanVsHuman));
    }


    [Test]
    public void Scheduling_HumanVsAi_SchedulesOnlyWhenPlayerTwoIsToMove()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[2, 2] = -1;
        var controller = new GameController(board, currentPlayer: 1, gameMode: GameMode.HumanVsAI);

        Assert.That(controller.ShouldScheduleAiTurn(), Is.False);

        controller.ApplyMove(new Move((5, 1), (4, 0)));

        Assert.That(controller.CurrentPlayer, Is.EqualTo(-1));
        Assert.That(controller.ShouldScheduleAiTurn(), Is.True);
    }

    [Test]
    public void Scheduling_AiVsAi_ContinuesAutomaticLoop()
    {
        var controller = new GameController(TestBoards.InitialPosition(), gameMode: GameMode.AIVsAI);

        Assert.That(controller.ShouldScheduleAiTurn(), Is.True);
        Assert.That(controller.ShouldContinueAutomaticAiLoop(), Is.True);
    }


    [Test]
    public void ApplyMove_WhenCaptureExists_RejectsQuietMoveFromAnotherPiece()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[5, 5] = 1;
        var controller = new GameController(board, 1, GameMode.HumanVsHuman);

        GameTurnResult result = controller.ApplyMove(new Move((5, 5), (4, 4)));

        Assert.That(result.MoveApplied, Is.False);
        Assert.That(controller.Board[5, 5], Is.EqualTo(1));
        Assert.That(controller.CurrentPlayer, Is.EqualTo(1));
    }

    [Test]
    public void ApplyMove_WhenLongerCaptureExists_RejectsShorterCapture()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[2, 4] = -1;
        board[5, 5] = 1;
        board[4, 6] = -1;
        var controller = new GameController(board, 1, GameMode.HumanVsHuman);

        GameTurnResult result = controller.ApplyMove(new Move((5, 5), (3, 7), new() { (4, 6) }));

        Assert.That(result.MoveApplied, Is.False);
        Assert.That(controller.Board[5, 5], Is.EqualTo(1));
        Assert.That(controller.Board[4, 6], Is.EqualTo(-1));
        Assert.That(controller.CurrentPlayer, Is.EqualTo(1));
    }

}
