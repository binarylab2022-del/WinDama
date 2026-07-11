using System.Collections.Generic;
using System.Linq;
using WinDama.Core;

namespace WinDama
{
    /// <summary>
    /// Legacy facade kept for compatibility with older code.
    /// The actual game-state and turn-management logic now lives in WinDama.Core.GameController.
    /// </summary>
    public class CheckersGame
    {
        private readonly MoveGenerator moveGenerator = new MoveGenerator();
        private readonly GameController controller;
        private readonly Stack<WinDama.Core.GameSnapshot> undoStack = new Stack<WinDama.Core.GameSnapshot>();
        private readonly Stack<WinDama.Core.GameSnapshot> redoStack = new Stack<WinDama.Core.GameSnapshot>();

        public CheckersGame()
        {
            BoardSize = 8;
            controller = new GameController(GameController.CreateInitialBoard(), currentPlayer: 1, gameMode: GameMode.HumanVsHuman, moveGenerator: moveGenerator);
        }

        public int[,] Board => controller.Board;
        public int BoardSize { get; }
        public int CurrentPlayer => controller.CurrentPlayer;
        public bool IsDama { get; private set; }
        public bool IsGameOver => controller.IsGameOver;
        public List<Move> MoveHistory => controller.MoveHistory.ToList();

        public List<Move> GetDamaMoves(int[,] board, int x, int y, bool forCaptureOnly = true)
        {
            return moveGenerator.GetDamaMoves(board, x, y, forCaptureOnly);
        }

        public List<Move> GetDamaMovesOrCaptures(int[,] board, int x, int y, int dx = 0, int dy = 0)
        {
            int piece = board[x, y];
            if (piece == 0)
            {
                return new List<Move>();
            }

            int player = piece > 0 ? 1 : -1;
            return moveGenerator.GetSquareCapturesOrMoves(board, player, x, y);
        }

        public List<Move> GetPieceAndDamaCaptures(int[,] board, int x, int y)
        {
            int piece = board[x, y];
            if (piece == 0)
            {
                return new List<Move>();
            }

            int player = piece > 0 ? 1 : -1;
            return moveGenerator.GetSquareCapturesOrMoves(board, player, x, y)
                .Where(move => move.CapturedPieces.Count > 0)
                .ToList();
        }

        public List<Move> GetPieceAndDamaMovesOrCaptures(int[,] board, int x, int y)
        {
            int piece = board[x, y];
            if (piece == 0)
            {
                return new List<Move>();
            }

            int player = piece > 0 ? 1 : -1;
            return moveGenerator.GetSquareCapturesOrMoves(board, player, x, y);
        }

        public List<Move> GetPieceMovesOrCaptures(int[,] board, int x, int y)
        {
            int piece = board[x, y];
            if (piece == 0)
            {
                return new List<Move>();
            }

            int player = piece > 0 ? 1 : -1;
            return moveGenerator.GetSquareCapturesOrMoves(board, player, x, y);
        }

        public List<Move> GetPlayerCapturesOrMoves(int[,] board, int currentPlayer)
        {
            return moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer);
        }

        public List<Move> GetSimplePieceMoves(int[,] board, int x, int y, bool forCaptureOnly = true)
        {
            int currentPlayer = board[x, y] > 0 ? 1 : -1;
            return moveGenerator.GetSimplePieceMoves(board, x, y, currentPlayer, forCaptureOnly);
        }

        public List<Move> GetSquareCapturesOrMoves(int[,] board, int currentPlayer, int pieceRow, int pieceColumn)
        {
            return moveGenerator.GetSquareCapturesOrMoves(board, currentPlayer, pieceRow, pieceColumn);
        }

        public List<Move> GetValidMoves(int player)
        {
            return controller.GetLegalMoves(player);
        }

        public bool MakeMove(int startX, int startY, int endX, int endY)
        {
            Move? move = controller.GetCurrentLegalMoves()
                .FirstOrDefault(m => m.Start == (startX, startY) && m.End == (endX, endY));

            if (move == null)
            {
                return false;
            }

            undoStack.Push(controller.CreateSnapshot());
            redoStack.Clear();
            GameTurnResult result = controller.ApplyMove(move);
            return result.MoveApplied;
        }

        public void UndoMove()
        {
            if (undoStack.Count == 0)
            {
                return;
            }

            redoStack.Push(controller.CreateSnapshot());
            controller.RestoreSnapshot(undoStack.Pop());
        }

        public void RedoMove()
        {
            if (redoStack.Count == 0)
            {
                return;
            }

            undoStack.Push(controller.CreateSnapshot());
            controller.RestoreSnapshot(redoStack.Pop());
        }
    }
}
