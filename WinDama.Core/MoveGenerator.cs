using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Central source of truth for Spanish checkers move generation.
///
/// Rule priority enforced here:
/// 1. If at least one capture exists anywhere, all quiet moves are illegal.
/// 2. If captures exist, only the longest capture / multi-capture sequences are legal.
/// 3. Piece-specific queries still respect the global mandatory-capture rule.
///
/// The UI, game controller, evaluation, and AI should call this class instead of
/// carrying their own copies of move-generation logic.
/// </summary>
public class MoveGenerator
{
    public List<Move> GetPlayerCapturesOrMoves(int[,] board, int currentPlayer)
    {
        List<Move> allCaptures = GetPlayerCapturesOnly(board, currentPlayer);
        if (allCaptures.Any())
        {
            return FilterMovesWithMaxCaptures(allCaptures);
        }

        return GetPlayerQuietMovesOnly(board, currentPlayer);
    }

    /// <summary>
    /// Returns the legal moves for a selected square while respecting the global
    /// mandatory-capture and longest-capture rules.
    ///
    /// This method must not return quiet moves when another piece can capture.
    /// It must also return no moves for a piece whose capture sequence is shorter
    /// than the globally longest available capture sequence.
    /// </summary>
    public List<Move> GetSquareCapturesOrMoves(int[,] board, int currentPlayer, int pieceRow, int pieceColumn)
    {
        if (!IsInsideBoard(pieceRow, pieceColumn))
        {
            return new List<Move>();
        }

        int piece = board[pieceRow, pieceColumn];
        if (piece != currentPlayer && piece != 2 * currentPlayer)
        {
            return new List<Move>();
        }

        List<Move> legalMoves = GetPlayerCapturesOrMoves(board, currentPlayer);
        return legalMoves
            .Where(move => move.Start.Item1 == pieceRow && move.Start.Item2 == pieceColumn)
            .ToList();
    }

    /// <summary>
    /// Returns only capture sequences for the given player, before global longest-capture filtering.
    /// </summary>
    public List<Move> GetPlayerCapturesOnly(int[,] board, int currentPlayer)
    {
        List<Move> allCaptures = new List<Move>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                int piece = board[x, y];
                if (piece == currentPlayer || piece == 2 * currentPlayer)
                {
                    allCaptures.AddRange(GetCapturesForPiece(board, currentPlayer, x, y));
                }
            }
        }

        return allCaptures;
    }

    /// <summary>
    /// Returns only quiet moves for the given player. Call this only after confirming
    /// that the player has no captures anywhere on the board.
    /// </summary>
    public List<Move> GetPlayerQuietMovesOnly(int[,] board, int currentPlayer)
    {
        List<Move> allMoves = new List<Move>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                int piece = board[x, y];
                if (piece == currentPlayer || piece == 2 * currentPlayer)
                {
                    allMoves.AddRange(GetQuietMovesForPiece(board, currentPlayer, x, y));
                }
            }
        }

        return allMoves;
    }

    /// <summary>
    /// Returns capture sequences for one piece only, filtered to the longest sequences
    /// available for that same piece. This intentionally ignores captures available
    /// to other pieces, so it is suitable for checking continuation from the landing
    /// square during move execution.
    /// </summary>
    public List<Move> GetCapturesForPiece(int[,] board, int currentPlayer, int pieceRow, int pieceColumn)
    {
        if (!IsInsideBoard(pieceRow, pieceColumn))
        {
            return new List<Move>();
        }

        int piece = board[pieceRow, pieceColumn];
        if (piece != currentPlayer && piece != 2 * currentPlayer)
        {
            return new List<Move>();
        }

        List<Move> captures = MathAbs(piece) == 1
            ? GetSimplePieceMoves(board, pieceRow, pieceColumn, currentPlayer, true)
            : GetDamaMoves(board, pieceRow, pieceColumn, true);

        return FilterMovesWithMaxCaptures(captures);
    }

    public List<Move> GetQuietMovesForPiece(int[,] board, int currentPlayer, int pieceRow, int pieceColumn)
    {
        if (!IsInsideBoard(pieceRow, pieceColumn))
        {
            return new List<Move>();
        }

        int piece = board[pieceRow, pieceColumn];
        if (piece != currentPlayer && piece != 2 * currentPlayer)
        {
            return new List<Move>();
        }

        return MathAbs(piece) == 1
            ? GetSimplePieceMoves(board, pieceRow, pieceColumn, currentPlayer, false)
                .Where(move => move.CapturedPieces.Count == 0)
                .ToList()
            : GetDamaMoves(board, pieceRow, pieceColumn, false)
                .Where(move => move.CapturedPieces.Count == 0)
                .ToList();
    }

    public bool HasAnyCapture(int[,] board, int currentPlayer)
    {
        return GetPlayerCapturesOnly(board, currentPlayer).Any();
    }

    public List<Move> GetSimplePieceMoves(int[,] board, int x, int y, int currentPlayer, bool forCaptureOnly = true)
    {
        List<Move> capturingMoves = new List<Move>();
        List<Move> possibleMoves = new List<Move>();

        int direction = board[x, y] > 0 ? -1 : 1;
        int[][] directions = new int[][]
        {
            new int[] { direction, -1 },
            new int[] { direction, 1 }
        };

        foreach (var dir in directions)
        {
            int newX = x + dir[0];
            int newY = y + dir[1];

            if (IsInsideBoard(newX, newY) && board[newX, newY] == 0 && !forCaptureOnly)
            {
                possibleMoves.Add(new Move((x, y), (newX, newY)));
            }

            int captureX = x + 2 * dir[0];
            int captureY = y + 2 * dir[1];
            if (IsInsideBoard(captureX, captureY) && board[captureX, captureY] == 0 &&
                IsInsideBoard(newX, newY) &&
                (board[newX, newY] == -currentPlayer || board[newX, newY] == -2 * currentPlayer))
            {
                Move captureMove = new Move((x, y), (captureX, captureY), new List<(int, int)> { (newX, newY) });
                int[,] newBoard = (int[,])board.Clone();
                newBoard[captureX, captureY] = newBoard[x, y];
                newBoard[x, y] = 0;
                newBoard[newX, newY] = 0;

                List<Move> subsequentCaptures = GetSimplePieceMoves(newBoard, captureX, captureY, currentPlayer, true);
                if (subsequentCaptures.Any())
                {
                    foreach (var subCapture in subsequentCaptures)
                    {
                        Move combinedMove = new Move(captureMove.Start, subCapture.End, new List<(int, int)>(captureMove.CapturedPieces));
                        combinedMove.CapturedPieces.AddRange(subCapture.CapturedPieces);
                        capturingMoves.Add(combinedMove);
                    }
                }
                else
                {
                    capturingMoves.Add(captureMove);
                }
            }
        }

        return capturingMoves.Any() ? FilterMovesWithMaxCaptures(capturingMoves) : possibleMoves;
    }

    public List<Move> GetDamaMoves(int[,] board, int x, int y, bool forCaptureOnly = true, List<(int, int)>? previouslyCaptured = null, int[]? lastDirection = null)
    {
        previouslyCaptured = previouslyCaptured ?? new List<(int, int)>();
        List<Move> capturingMoves = new List<Move>();
        List<Move> possibleMoves = new List<Move>();

        int opponentPiece = board[x, y] > 0 ? -1 : 1;
        int opponentDama = board[x, y] > 0 ? -2 : 2;

        int[][] directions = new int[][]
        {
            new int[] { -1, -1 },
            new int[] { -1, 1 },
            new int[] { 1, -1 },
            new int[] { 1, 1 }
        };

        foreach (var direction in directions)
        {
            if (lastDirection != null && direction[0] == -lastDirection[0] && direction[1] == -lastDirection[1])
            {
                continue;
            }

            int dx = direction[0];
            int dy = direction[1];
            int newX = x + dx;
            int newY = y + dy;
            bool foundOpponent = false;
            int opponentX = -1;
            int opponentY = -1;

            while (IsInsideBoard(newX, newY))
            {
                if (board[newX, newY] == 0)
                {
                    if (foundOpponent)
                    {
                        var capturedCoord = (opponentX, opponentY);
                        if (previouslyCaptured.Contains(capturedCoord))
                        {
                            break;
                        }

                        Move captureMove = new Move((x, y), (newX, newY), new List<(int, int)> { capturedCoord });
                        int[,] newBoard = (int[,])board.Clone();
                        newBoard[newX, newY] = newBoard[x, y];
                        newBoard[x, y] = 0;
                        newBoard[opponentX, opponentY] = 0;

                        var newCaptured = new List<(int, int)>(previouslyCaptured) { capturedCoord };
                        var subsequentCaptures = GetDamaMoves(newBoard, newX, newY, true, newCaptured, direction);
                        if (subsequentCaptures.Any())
                        {
                            foreach (var subCapture in subsequentCaptures)
                            {
                                Move combinedMove = new Move(captureMove.Start, subCapture.End, new List<(int, int)>(captureMove.CapturedPieces));
                                combinedMove.CapturedPieces.AddRange(subCapture.CapturedPieces);
                                capturingMoves.Add(combinedMove);
                            }
                        }
                        else
                        {
                            capturingMoves.Add(captureMove);
                        }
                    }
                    else if (!forCaptureOnly)
                    {
                        possibleMoves.Add(new Move((x, y), (newX, newY)));
                    }
                }
                else if (board[newX, newY] == opponentPiece || board[newX, newY] == opponentDama)
                {
                    if (foundOpponent)
                    {
                        break;
                    }

                    foundOpponent = true;
                    opponentX = newX;
                    opponentY = newY;
                }
                else
                {
                    break;
                }

                newX += dx;
                newY += dy;
            }
        }

        return capturingMoves.Any() ? FilterMovesWithMaxCaptures(capturingMoves) : possibleMoves;
    }

    public List<Move> FilterMovesWithMaxCaptures(List<Move> moves)
    {
        if (moves.Count == 0)
        {
            return moves;
        }

        int maxCaptures = moves.Max(m => m.CapturedPieces.Count);
        return moves.Where(m => m.CapturedPieces.Count == maxCaptures).ToList();
    }

    private bool IsInsideBoard(int x, int y)
    {
        return x >= 0 && x < 8 && y >= 0 && y < 8;
    }

    // Avoid adding a System dependency only for Math.Abs in this low-level helper.
    private static int MathAbs(int value)
    {
        return value < 0 ? -value : value;
    }
}
