using System;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Serializable board position used for manual test cases, engine debugging,
/// and saved tactical positions. The board is stored as jagged rows because
/// System.Text.Json does not serialize multi-dimensional arrays by default.
/// </summary>
public sealed class TestPosition
{
    public string Name { get; set; } = "Untitled position";
    public string Notes { get; set; } = string.Empty;
    public int[][] BoardRows { get; set; } = CreateEmptyRows();
    public int CurrentPlayer { get; set; } = 1;
    public GameMode GameMode { get; set; } = GameMode.HumanVsHuman;
    public bool IsGameOver { get; set; }

    /// <summary>
    /// Optional benchmark expectation. Use the same notation produced by
    /// MoveNotation.Format, for example: "6,2x4,4 (1)".
    /// </summary>
    public string ExpectedBestMove { get; set; } = string.Empty;

    /// <summary>
    /// Optional list of acceptable best moves. Useful when several moves are
    /// equally correct, such as equal longest captures.
    /// </summary>
    public string[] ExpectedBestMoves { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional list of acceptable first PV moves. If omitted, ExpectedBestMove(s)
    /// are used as the PV-first expectation.
    /// </summary>
    public string[] ExpectedPvFirstMoves { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional evaluation-sign expectation for benchmark scoring. Supported
    /// values: positive, negative, zero, nonnegative, nonpositive, any.
    /// </summary>
    public string ExpectedEvaluationSign { get; set; } = string.Empty;

    public int? ExpectedLegalMoveCount { get; set; }
    public int? ExpectedCaptureMoveCount { get; set; }
    public int? ExpectedMinBestMoveCaptureCount { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public static TestPosition FromState(string? name, string? notes, int[,] board, int currentPlayer, GameMode gameMode, bool isGameOver = false)
    {
        return new TestPosition
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled position" : name.Trim(),
            Notes = notes ?? string.Empty,
            BoardRows = ToRows(board),
            CurrentPlayer = NormalizePlayer(currentPlayer),
            GameMode = gameMode,
            IsGameOver = isGameOver,
            CreatedUtc = DateTime.UtcNow
        };
    }

    public int[,] ToBoard()
    {
        Validate();
        int[,] board = new int[8, 8];
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                board[row, column] = BoardRows[row][column];
            }
        }

        return board;
    }

    public void Validate()
    {
        if (BoardRows == null || BoardRows.Length != 8)
        {
            throw new InvalidOperationException("A test position must contain exactly 8 board rows.");
        }

        for (int row = 0; row < 8; row++)
        {
            if (BoardRows[row] == null || BoardRows[row].Length != 8)
            {
                throw new InvalidOperationException($"Board row {row + 1} must contain exactly 8 columns.");
            }

            for (int column = 0; column < 8; column++)
            {
                int value = BoardRows[row][column];
                if (value is not (0 or 1 or 2 or -1 or -2))
                {
                    throw new InvalidOperationException($"Invalid piece value {value} at row {row + 1}, column {column + 1}.");
                }
            }
        }

        CurrentPlayer = NormalizePlayer(CurrentPlayer);
        ExpectedBestMove = ExpectedBestMove?.Trim() ?? string.Empty;
        ExpectedBestMoves = NormalizeExpectedMoves(ExpectedBestMoves);
        ExpectedPvFirstMoves = NormalizeExpectedMoves(ExpectedPvFirstMoves);
        ExpectedEvaluationSign = (ExpectedEvaluationSign ?? string.Empty).Trim().ToLowerInvariant();

        if (ExpectedLegalMoveCount < 0)
        {
            throw new InvalidOperationException("Expected legal-move count cannot be negative.");
        }

        if (ExpectedCaptureMoveCount < 0)
        {
            throw new InvalidOperationException("Expected capture-move count cannot be negative.");
        }

        if (ExpectedMinBestMoveCaptureCount < 0)
        {
            throw new InvalidOperationException("Expected minimum best-move capture count cannot be negative.");
        }
    }

    private static int[][] ToRows(int[,] board)
    {
        if (board == null || board.GetLength(0) != 8 || board.GetLength(1) != 8)
        {
            throw new ArgumentException("The board must be an 8x8 array.", nameof(board));
        }

        int[][] rows = CreateEmptyRows();
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                rows[row][column] = board[row, column];
            }
        }

        return rows;
    }

    private static int[][] CreateEmptyRows()
    {
        return Enumerable.Range(0, 8)
            .Select(_ => new int[8])
            .ToArray();
    }

    private static int NormalizePlayer(int player)
    {
        return player >= 0 ? 1 : -1;
    }

    private static string[] NormalizeExpectedMoves(string[]? moves)
    {
        return (moves ?? Array.Empty<string>())
            .Where(move => !string.IsNullOrWhiteSpace(move))
            .Select(move => move.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
