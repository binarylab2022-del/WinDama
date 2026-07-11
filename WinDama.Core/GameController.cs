using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// UI-independent controller for the real game state.
///
/// Responsibilities kept here:
/// - board state;
/// - current-player state;
/// - move validation against the move generator;
/// - real move execution;
/// - turn switching;
/// - game-over detection;
/// - deciding whether a side is controlled by the AI in the selected mode.
///
/// Responsibilities deliberately not kept here:
/// - WPF drawing;
/// - timers/clocks;
/// - MessageBox/dialogs;
/// - starting background AI tasks.
/// </summary>
public sealed class GameController
{
    private readonly MoveGenerator moveGenerator;
    private readonly List<Move> moveHistory = new();

    public GameController()
        : this(CreateInitialBoard(), 1, GameMode.HumanVsAI, new MoveGenerator())
    {
    }

    public GameController(int[,] board, int currentPlayer = 1, GameMode gameMode = GameMode.HumanVsAI, MoveGenerator? moveGenerator = null)
    {
        this.moveGenerator = moveGenerator ?? new MoveGenerator();
        Board = (int[,])board.Clone();
        CurrentPlayer = NormalizePlayer(currentPlayer);
        GameMode = gameMode;
        Status = EvaluateStatusForSideToMove();
    }

    public int[,] Board { get; private set; }
    public int CurrentPlayer { get; private set; } = 1;
    public GameMode GameMode { get; private set; } = GameMode.HumanVsAI;
    public GameStatus Status { get; private set; } = GameStatus.Ongoing;
    public bool IsGameOver => Status != GameStatus.Ongoing;
    public Move? LastMove { get; private set; }
    public IReadOnlyList<Move> MoveHistory => moveHistory.Select(CloneMove).Where(m => m != null).Cast<Move>().ToList();
    public GameState State => new GameState(Board, CurrentPlayer, GameMode, Status, LastMove, moveHistory);

    public static int[,] CreateInitialBoard()
    {
        return new int[8, 8]
        {
            { -1, 0, -1, 0, -1, 0, -1, 0 },
            { 0, -1, 0, -1, 0, -1, 0, -1 },
            { -1, 0, -1, 0, -1, 0, -1, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 1, 0, 1, 0, 1, 0, 1 },
            { 1, 0, 1, 0, 1, 0, 1, 0 },
            { 0, 1, 0, 1, 0, 1, 0, 1 }
        };
    }

    public void NewGame(int[,]? initialBoard = null, int startingPlayer = 1)
    {
        Board = (int[,])(initialBoard ?? CreateInitialBoard()).Clone();
        CurrentPlayer = NormalizePlayer(startingPlayer);
        Status = EvaluateStatusForSideToMove();
        LastMove = null;
        moveHistory.Clear();
    }

    public void SetGameMode(GameMode mode)
    {
        GameMode = mode;
    }

    public bool IsAiControlledPlayer(int player)
    {
        player = NormalizePlayer(player);
        return GameMode switch
        {
            GameMode.HumanVsAI => player == -1,
            GameMode.HumanVsHuman => false,
            GameMode.AIVsAI => true,
            _ => false
        };
    }

    public bool IsHumanControlledPlayer(int player)
    {
        return !IsAiControlledPlayer(player);
    }

    public bool IsCurrentSideAiControlled()
    {
        return IsAiControlledPlayer(CurrentPlayer);
    }

    public bool ShouldScheduleAiTurn()
    {
        return !IsGameOver && IsCurrentSideAiControlled();
    }

    public bool ShouldContinueAutomaticAiLoop()
    {
        return !IsGameOver && GameMode == GameMode.AIVsAI && IsCurrentSideAiControlled();
    }

    public List<Move> GetLegalMoves(int player)
    {
        return moveGenerator.GetPlayerCapturesOrMoves(Board, NormalizePlayer(player));
    }

    public List<Move> GetCurrentLegalMoves()
    {
        return GetLegalMoves(CurrentPlayer);
    }

    public List<Move> GetLegalMovesForPiece(int row, int column)
    {
        return GetCurrentLegalMoves()
            .Where(move => move.Start.Item1 == row && move.Start.Item2 == column)
            .ToList();
    }

    public bool HasAnyValidMoves(int player)
    {
        return GetLegalMoves(player).Count > 0;
    }

    public bool IsCurrentPlayerPiece(int piece)
    {
        return piece == CurrentPlayer || piece == 2 * CurrentPlayer;
    }

    public GameStatus EvaluateStatusForSideToMove()
    {
        if (!HasAnyValidMoves(CurrentPlayer))
        {
            return CurrentPlayer == 1 ? GameStatus.PlayerTwoWon : GameStatus.PlayerOneWon;
        }

        if (IsDraw())
        {
            return GameStatus.Draw;
        }

        return GameStatus.Ongoing;
    }

    public GameTurnResult ApplyMove(Move move)
    {
        if (move == null)
        {
            return BuildResult(false, false, CurrentPlayer, null, "No move was supplied.");
        }

        if (IsGameOver)
        {
            return BuildResult(false, false, CurrentPlayer, move, "The game is already over.");
        }

        int playerBeforeMove = CurrentPlayer;
        Move? legalMove = FindMatchingLegalMove(move);
        if (legalMove == null)
        {
            return BuildResult(false, false, playerBeforeMove, move, "Illegal move.");
        }

        MoveExecutionResult executionResult = MoveExecutor.ApplyMoveInPlace(Board, legalMove, moveGenerator);
        LastMove = CloneMove(legalMove);
        moveHistory.Add(CloneMove(legalMove)!);

        bool turnSwitched = executionResult == MoveExecutionResult.TurnShouldSwitch;
        if (turnSwitched)
        {
            CurrentPlayer = -CurrentPlayer;
            Status = EvaluateStatusForSideToMove();
        }
        else
        {
            Status = GameStatus.Ongoing;
        }

        return BuildResult(true, turnSwitched, playerBeforeMove, legalMove, BuildMoveMessage(turnSwitched));
    }

    public GameSnapshot CreateSnapshot()
    {
        return new GameSnapshot(Board, CurrentPlayer, IsGameOver, LastMove, moveHistory);
    }

    public void RestoreSnapshot(GameSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        Board = snapshot.CloneBoard();
        CurrentPlayer = NormalizePlayer(snapshot.CurrentPlayer);
        LastMove = CloneMove(snapshot.LastMove);
        moveHistory.Clear();
        moveHistory.AddRange(snapshot.MoveHistory.Select(CloneMove).Where(m => m != null).Cast<Move>());
        Status = snapshot.IsGameOver ? EvaluateFinishedStatusFromSnapshot() : EvaluateStatusForSideToMove();
    }

    public void RestoreState(int[,] board, int currentPlayer, bool isGameOver, Move? lastMove = null, IEnumerable<Move>? history = null)
    {
        Board = (int[,])board.Clone();
        CurrentPlayer = NormalizePlayer(currentPlayer);
        LastMove = CloneMove(lastMove);
        moveHistory.Clear();
        if (history != null)
        {
            moveHistory.AddRange(history.Select(CloneMove).Where(m => m != null).Cast<Move>());
        }

        Status = isGameOver ? EvaluateFinishedStatusFromSnapshot() : EvaluateStatusForSideToMove();
    }

    public string GetStatusMessage()
    {
        return Status switch
        {
            GameStatus.PlayerOneWon => "Player 1 wins.",
            GameStatus.PlayerTwoWon => "Player 2 wins.",
            GameStatus.Draw => "The game is a draw.",
            _ => string.Empty
        };
    }

    public static string GetPlayerLabel(int player)
    {
        return NormalizePlayer(player) == 1 ? "Player 1" : "Player 2";
    }

    private Move? FindMatchingLegalMove(Move move)
    {
        return GetCurrentLegalMoves().FirstOrDefault(legalMove => AreSameMove(legalMove, move));
    }

    private static bool AreSameMove(Move left, Move right)
    {
        return left.Start.Equals(right.Start)
            && left.End.Equals(right.End)
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }

    private GameTurnResult BuildResult(bool applied, bool turnSwitched, int playerBeforeMove, Move? move, string message)
    {
        return new GameTurnResult
        {
            MoveApplied = applied,
            TurnSwitched = turnSwitched,
            PlayerBeforeMove = playerBeforeMove,
            CurrentPlayer = CurrentPlayer,
            Move = CloneMove(move),
            Status = Status,
            Message = message
        };
    }

    private string BuildMoveMessage(bool turnSwitched)
    {
        if (Status != GameStatus.Ongoing)
        {
            return GetStatusMessage();
        }

        return turnSwitched
            ? $"{GetPlayerLabel(CurrentPlayer)} to move."
            : $"{GetPlayerLabel(CurrentPlayer)} must continue capturing.";
    }

    private GameStatus EvaluateFinishedStatusFromSnapshot()
    {
        GameStatus evaluated = EvaluateStatusForSideToMove();
        return evaluated == GameStatus.Ongoing ? GameStatus.Draw : evaluated;
    }

    private static bool IsDraw()
    {
        // Future extension point: repetition, fifty-move-like rule, king-only endings, etc.
        return false;
    }

    private static int NormalizePlayer(int player)
    {
        return player >= 0 ? 1 : -1;
    }

    private static Move? CloneMove(Move? move)
    {
        return move == null
            ? null
            : new Move(move.Start, move.End, new List<(int, int)>(move.CapturedPieces));
    }
}
