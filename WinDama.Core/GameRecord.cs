using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Serializable full-game record. It is intentionally independent from WPF and
/// can be used later as training data for evaluation tuning or NNUE experiments.
/// </summary>
public sealed class GameRecord
{
    public string GameId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public string EventName { get; init; } = "WinDama game";
    public string PlayerOneProfile { get; init; } = string.Empty;
    public string PlayerTwoProfile { get; init; } = string.Empty;
    public int Winner { get; init; }
    public string WinnerProfile { get; init; } = "Draw";
    public string ResultText { get; init; } = "1/2-1/2";
    public string StopReason { get; init; } = string.Empty;
    public int InitialPlayer { get; init; } = 1;
    public int[][] InitialBoard { get; init; } = BoardToRows(GameController.CreateInitialBoard());
    public List<GameMoveRecord> Moves { get; init; } = new List<GameMoveRecord>();

    public int PlyCount => Moves.Count;
    public int MoveCount => (PlyCount + 1) / 2;

    public int[,] CloneInitialBoard()
    {
        return RowsToBoard(InitialBoard);
    }

    public static GameRecord FromTournamentGame(EvaluationTournamentGameResult game)
    {
        return new GameRecord
        {
            EventName = "Evaluation tournament",
            PlayerOneProfile = game.PlayerOneProfile,
            PlayerTwoProfile = game.PlayerTwoProfile,
            Winner = game.Winner,
            WinnerProfile = game.WinnerProfile,
            ResultText = game.ResultText,
            StopReason = game.StopReason,
            InitialPlayer = 1,
            InitialBoard = BoardToRows(GameController.CreateInitialBoard()),
            Moves = game.MoveRecords.ToList()
        };
    }

    public static int[][] BoardToRows(int[,] board)
    {
        int rows = board.GetLength(0);
        int columns = board.GetLength(1);
        int[][] result = new int[rows][];
        for (int row = 0; row < rows; row++)
        {
            result[row] = new int[columns];
            for (int column = 0; column < columns; column++)
            {
                result[row][column] = board[row, column];
            }
        }

        return result;
    }

    public static int[,] RowsToBoard(int[][] rows)
    {
        if (rows == null || rows.Length == 0)
        {
            return GameController.CreateInitialBoard();
        }

        int rowCount = rows.Length;
        int columnCount = rows[0].Length;
        int[,] board = new int[rowCount, columnCount];
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                board[row, column] = rows[row][column];
            }
        }

        return board;
    }
}
