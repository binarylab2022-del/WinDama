using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Position evaluator for Spanish/Algerian checkers.
/// Scores are always returned from the point of view of currentPlayer:
/// positive is good for currentPlayer, negative is good for the opponent.
/// </summary>
public class Evaluation
{
    private readonly EvaluationWeights weights;

    private readonly MoveGenerator moveGenerator = new MoveGenerator();

    //-----------------------------------------------------------------------
    public Evaluation(int[,] board, int currentPlayer)
        : this()
    {
    }

    public Evaluation()
        : this(EvaluationWeights.Default)
    {
    }

    public Evaluation(EvaluationWeights weights)
    {
        this.weights = weights ?? EvaluationWeights.Default;
    }

    public List<Move> GetPlayerCapturesOrMoves(int[,] board, int currentPlayer)
    {
        return moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer);
    }

    //-----------------------------------------------------------------------
    public virtual int EvaluateBoard(int[,] board, int currentPlayer)
    {
        return EvaluateBreakdown(board, currentPlayer).Total;
    }

    //-----------------------------------------------------------------------
    public virtual EvaluationBreakdown EvaluateBreakdown(int[,] board, int currentPlayer)
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        currentPlayer = currentPlayer >= 0 ? 1 : -1;

        EvaluationBreakdown breakdown = new EvaluationBreakdown
        {
            Material = EvaluateMaterial(board, currentPlayer),
            Positioning = EvaluatePositioning(board, currentPlayer),
            Advancement = EvaluateAdvancement(board, currentPlayer),
            Mobility = EvaluateMobility(board, currentPlayer),
            CapturePotential = EvaluateCapturePotential(board, currentPlayer),
            Vulnerability = EvaluateVulnerability(board, currentPlayer),
            PromotionThreats = EvaluatePromotionThreats(board, currentPlayer),
            Protection = EvaluateProtection(board, currentPlayer),
            Tempo = weights.TempoBonus
        };

        breakdown.Total = breakdown.Material
            + breakdown.Positioning
            + breakdown.Advancement
            + breakdown.Mobility
            + breakdown.CapturePotential
            + breakdown.Vulnerability
            + breakdown.PromotionThreats
            + breakdown.Protection
            + breakdown.Tempo;

        return breakdown;
    }

    //-----------------------------------------------------------------------
    private int EvaluateMaterial(int[,] board, int currentPlayer)
    {
        int score = 0;

        foreach ((int row, int column) in Squares())
        {
            int piece = board[row, column];
            if (piece == 0)
            {
                continue;
            }

            int value = Math.Abs(piece) == 2 ? weights.DamaValue : weights.ManValue;
            score += Math.Sign(piece) == currentPlayer ? value : -value;
        }

        return score;
    }

    //-----------------------------------------------------------------------
    private int EvaluatePositioning(int[,] board, int currentPlayer)
    {
        int score = 0;

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

            if (row >= 2 && row <= 5 && column >= 2 && column <= 5)
            {
                score += sign * (isDama ? weights.CenterDamaBonus : weights.CenterManBonus);
            }

            if (row == 0 || row == 7 || column == 0 || column == 7)
            {
                score += sign * weights.EdgeSafetyBonus;
            }

            if (!isDama && IsHomeRow(row, owner))
            {
                score += sign * weights.BackRowGuardBonus;
            }
        }

        return score;
    }

    //-----------------------------------------------------------------------
    private int EvaluateAdvancement(int[,] board, int currentPlayer)
    {
        int score = 0;

        foreach ((int row, int column) in Squares())
        {
            int piece = board[row, column];
            if (piece == 0 || Math.Abs(piece) == 2)
            {
                continue;
            }

            int owner = Math.Sign(piece);
            int sign = owner == currentPlayer ? 1 : -1;
            int advancement = owner == 1 ? 7 - row : row;
            score += sign * advancement * weights.AdvancedManWeight;
        }

        return score;
    }

    //-----------------------------------------------------------------------
    private int EvaluateMobility(int[,] board, int currentPlayer)
    {
        List<Move> ownMoves = moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer);
        List<Move> opponentMoves = moveGenerator.GetPlayerCapturesOrMoves(board, -currentPlayer);

        int ownDamaMobility = CountDamaMoves(board, ownMoves);
        int opponentDamaMobility = CountDamaMoves(board, opponentMoves);

        return weights.MobilityWeight * (ownMoves.Count - opponentMoves.Count)
            + weights.DamaMobilityWeight * (ownDamaMobility - opponentDamaMobility);
    }

    //-----------------------------------------------------------------------
    private int EvaluateCapturePotential(int[,] board, int currentPlayer)
    {
        List<Move> ownCaptures = moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer)
            .Where(IsCapture)
            .ToList();
        List<Move> opponentCaptures = moveGenerator.GetPlayerCapturesOrMoves(board, -currentPlayer)
            .Where(IsCapture)
            .ToList();

        int ownCapturedPieces = ownCaptures.Sum(move => move.CapturedPieces.Count);
        int opponentCapturedPieces = opponentCaptures.Sum(move => move.CapturedPieces.Count);

        int ownBest = ownCaptures.Count == 0 ? 0 : ownCaptures.Max(move => move.CapturedPieces.Count);
        int opponentBest = opponentCaptures.Count == 0 ? 0 : opponentCaptures.Max(move => move.CapturedPieces.Count);

        return weights.WinLikeCaptureSwing * (ownCapturedPieces - opponentCapturedPieces)
            + weights.BestCaptureSequenceBonus * (ownBest - opponentBest);
    }

    //-----------------------------------------------------------------------
    private int EvaluateVulnerability(int[,] board, int currentPlayer)
    {
        // Vulnerability is intentionally a pure penalty: it measures how many
        // of currentPlayer's pieces are currently capturable by the opponent.
        // Positive tactical chances are handled separately by
        // EvaluateCapturePotential(...). Keeping the two concepts separate
        // avoids a reciprocal-capture position cancelling the vulnerability
        // penalty to zero.
        int score = 0;

        HashSet<(int, int)> ownPiecesCapturedByOpponent = GetCapturedSquares(board, -currentPlayer);
        foreach ((int row, int column) in ownPiecesCapturedByOpponent)
        {
            int piece = board[row, column];
            if (piece != 0 && Math.Sign(piece) == currentPlayer)
            {
                score -= Math.Abs(piece) == 2 ? weights.VulnerableDamaPenalty : weights.VulnerableManPenalty;
            }
        }

        return score;
    }

    //-----------------------------------------------------------------------
    private int EvaluatePromotionThreats(int[,] board, int currentPlayer)
    {
        int score = 0;

        foreach ((int row, int column) in Squares())
        {
            int piece = board[row, column];
            if (piece == 0 || Math.Abs(piece) == 2)
            {
                continue;
            }

            int owner = Math.Sign(piece);
            int sign = owner == currentPlayer ? 1 : -1;
            int promotionDistance = owner == 1 ? row : 7 - row;

            if (promotionDistance == 1)
            {
                score += sign * weights.PromotionThreatBonus;
            }
            else if (promotionDistance == 2)
            {
                score += sign * weights.NearPromotionBonus;
            }
        }

        return score;
    }

    //-----------------------------------------------------------------------
    private int EvaluateProtection(int[,] board, int currentPlayer)
    {
        int score = 0;

        foreach ((int row, int column) in Squares())
        {
            int piece = board[row, column];
            if (piece == 0 || Math.Abs(piece) == 2)
            {
                continue;
            }

            int owner = Math.Sign(piece);
            int sign = owner == currentPlayer ? 1 : -1;
            if (HasFriendlyDiagonalSupport(board, row, column, owner))
            {
                score += sign * weights.ProtectedManBonus;
            }
        }

        return score;
    }

    //-----------------------------------------------------------------------
    private HashSet<(int, int)> GetCapturedSquares(int[,] board, int player)
    {
        // Vulnerability is a local tactical feature, not a legal-move list.
        // Therefore count raw capture possibilities before global longest-capture
        // filtering. Otherwise a genuinely capturable piece can be missed when
        // another longer capture exists elsewhere on the board.
        return moveGenerator.GetPlayerCapturesOnly(board, player)
            .Where(IsCapture)
            .SelectMany(move => move.CapturedPieces)
            .ToHashSet();
    }

    //-----------------------------------------------------------------------
    private int CountDamaMoves(int[,] board, List<Move> moves)
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

    //-----------------------------------------------------------------------
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

    //-----------------------------------------------------------------------
    private static bool IsHomeRow(int row, int player)
    {
        return player == 1 ? row == 7 : row == 0;
    }

    //-----------------------------------------------------------------------
    private static bool IsCapture(Move move)
    {
        return move.CapturedPieces != null && move.CapturedPieces.Count > 0;
    }

    //-----------------------------------------------------------------------
    private static bool IsInside(int row, int column)
    {
        return row >= 0 && row < 8 && column >= 0 && column < 8;
    }

    //-----------------------------------------------------------------------
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

public sealed class EvaluationBreakdown
{
    public int Material { get; init; }
    public int Positioning { get; init; }
    public int Advancement { get; init; }
    public int Mobility { get; init; }
    public int CapturePotential { get; init; }
    public int Vulnerability { get; init; }
    public int PromotionThreats { get; init; }
    public int Protection { get; init; }
    public int Tempo { get; init; }
    public int Total { get; set; }

    public override string ToString()
    {
        return $"total {Total}, material {Material}, position {Positioning}, advance {Advancement}, mobility {Mobility}, capture {CapturePotential}, vulnerability {Vulnerability}, promotion {PromotionThreats}";
    }
}
