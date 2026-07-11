using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Converts a board position into a compact set of numeric features suitable
/// for a linear evaluator. Values are always from currentPlayer's point of view.
/// Positive features usually favor currentPlayer; negative values favor the opponent.
/// </summary>
public sealed class LinearEvaluationFeatureExtractor
{
    public static readonly string[] FeatureNames =
    {
        "man_balance",
        "dama_balance",
        "advancement",
        "center_control",
        "edge_safety",
        "back_row_guard",
        "mobility",
        "dama_mobility",
        "capture_count",
        "best_capture_length",
        "captured_material_available",
        "vulnerable_men",
        "vulnerable_damas",
        "protected_men",
        "promotion_threats",
        "near_promotion",
        "tempo"
    };

    private readonly MoveGenerator moveGenerator;

    public LinearEvaluationFeatureExtractor()
        : this(new MoveGenerator())
    {
    }

    public LinearEvaluationFeatureExtractor(MoveGenerator moveGenerator)
    {
        this.moveGenerator = moveGenerator ?? throw new ArgumentNullException(nameof(moveGenerator));
    }

    public FeatureVector Extract(int[,] board, int currentPlayer)
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        currentPlayer = currentPlayer == -1 ? -1 : 1;
        Dictionary<string, double> values = FeatureNames.ToDictionary(name => name, _ => 0.0, StringComparer.Ordinal);

        foreach ((int row, int column) in Squares())
        {
            int piece = board[row, column];
            if (piece == 0)
            {
                continue;
            }

            int owner = Math.Sign(piece);
            int sign = owner == currentPlayer ? 1 : -1;
            bool isDama = Math.Abs(piece) == 2;

            if (isDama)
            {
                values["dama_balance"] += sign;
            }
            else
            {
                values["man_balance"] += sign;
                values["advancement"] += sign * (owner == 1 ? 7 - row : row);

                if (IsHomeRow(row, owner))
                {
                    values["back_row_guard"] += sign;
                }

                if (HasFriendlyDiagonalSupport(board, row, column, owner))
                {
                    values["protected_men"] += sign;
                }

                int promotionDistance = owner == 1 ? row : 7 - row;
                if (promotionDistance == 1)
                {
                    values["promotion_threats"] += sign;
                }
                else if (promotionDistance == 2)
                {
                    values["near_promotion"] += sign;
                }
            }

            if (row >= 2 && row <= 5 && column >= 2 && column <= 5)
            {
                values["center_control"] += sign * (isDama ? 2.0 : 1.0);
            }

            if (row == 0 || row == 7 || column == 0 || column == 7)
            {
                values["edge_safety"] += sign;
            }
        }

        List<Move> ownMoves = moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer);
        List<Move> opponentMoves = moveGenerator.GetPlayerCapturesOrMoves(board, -currentPlayer);
        List<Move> ownCaptures = ownMoves.Where(IsCapture).ToList();
        List<Move> opponentCaptures = opponentMoves.Where(IsCapture).ToList();

        values["mobility"] = ownMoves.Count - opponentMoves.Count;
        values["dama_mobility"] = CountDamaMoves(board, ownMoves) - CountDamaMoves(board, opponentMoves);
        values["capture_count"] = ownCaptures.Sum(move => move.CapturedPieces.Count) - opponentCaptures.Sum(move => move.CapturedPieces.Count);
        values["best_capture_length"] = BestCaptureLength(ownCaptures) - BestCaptureLength(opponentCaptures);
        values["captured_material_available"] = CapturedMaterial(board, ownCaptures) - CapturedMaterial(board, opponentCaptures);

        HashSet<(int, int)> ownCapturedByOpponent = GetCapturedSquares(board, -currentPlayer);
        foreach ((int row, int column) in ownCapturedByOpponent)
        {
            int piece = board[row, column];
            if (piece != 0 && Math.Sign(piece) == currentPlayer)
            {
                if (Math.Abs(piece) == 2)
                {
                    values["vulnerable_damas"] -= 1.0;
                }
                else
                {
                    values["vulnerable_men"] -= 1.0;
                }
            }
        }

        values["tempo"] = 1.0;
        return new FeatureVector { Values = values };
    }

    private HashSet<(int, int)> GetCapturedSquares(int[,] board, int player)
    {
        return moveGenerator.GetPlayerCapturesOnly(board, player)
            .Where(IsCapture)
            .SelectMany(move => move.CapturedPieces)
            .ToHashSet();
    }

    private static int CountDamaMoves(int[,] board, IEnumerable<Move> moves)
    {
        int count = 0;
        foreach (Move move in moves)
        {
            int piece = board[move.Start.Item1, move.Start.Item2];
            if (Math.Abs(piece) == 2)
            {
                count++;
            }
        }

        return count;
    }

    private static int BestCaptureLength(IReadOnlyCollection<Move> captures)
    {
        return captures.Count == 0 ? 0 : captures.Max(move => move.CapturedPieces.Count);
    }

    private static int CapturedMaterial(int[,] board, IEnumerable<Move> captures)
    {
        int total = 0;
        foreach (Move move in captures)
        {
            foreach ((int row, int column) in move.CapturedPieces)
            {
                int piece = board[row, column];
                total += Math.Abs(piece) == 2 ? 4 : 1;
            }
        }

        return total;
    }

    private static bool IsCapture(Move move)
    {
        return move.CapturedPieces != null && move.CapturedPieces.Count > 0;
    }

    private static bool IsHomeRow(int row, int player)
    {
        return player == 1 ? row == 7 : row == 0;
    }

    private static bool HasFriendlyDiagonalSupport(int[,] board, int row, int column, int owner)
    {
        int[,] directions =
        {
            { -1, -1 }, { -1, 1 }, { 1, -1 }, { 1, 1 }
        };

        for (int i = 0; i < directions.GetLength(0); i++)
        {
            int r = row + directions[i, 0];
            int c = column + directions[i, 1];
            if (IsInside(r, c) && board[r, c] != 0 && Math.Sign(board[r, c]) == owner)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInside(int row, int column)
    {
        return row >= 0 && row < 8 && column >= 0 && column < 8;
    }

    private static IEnumerable<(int row, int column)> Squares()
    {
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                yield return (row, column);
            }
        }
    }
}
