using System;
using System.Collections.Generic;
using System.Linq;
using WinDama.Core;

namespace WinDama;

internal static class AnalysisTextFormatter
{
    public static string FormatNodesPerSecond(double nodesPerSecond)
    {
        if (double.IsNaN(nodesPerSecond) || double.IsInfinity(nodesPerSecond) || nodesPerSecond < 0)
        {
            return "-";
        }

        if (nodesPerSecond >= 1_000_000)
        {
            return $"{nodesPerSecond / 1_000_000:0.0}M";
        }

        if (nodesPerSecond >= 1_000)
        {
            return $"{nodesPerSecond / 1_000:0.0}k";
        }

        return $"{nodesPerSecond:0}";
    }

    public static string FormatTranspositionStats(long hits, long cutoffs, long stores, int entries, long killerCutoffs = 0, long historyUpdates = 0)
    {
        string heuristicText = killerCutoffs > 0 || historyUpdates > 0
            ? $", killer {killerCutoffs:N0}, hist {historyUpdates:N0}"
            : string.Empty;

        return $"TT hits {hits:N0}, cut {cutoffs:N0}, stores {stores:N0}, entries {entries:N0}{heuristicText}";
    }

    public static string FormatTopMoves(IReadOnlyList<SearchCandidate>? topMoves)
    {
        if (topMoves == null || topMoves.Count == 0)
        {
            return "-";
        }

        return string.Join(Environment.NewLine,
            topMoves.Take(5).Select(candidate =>
                $"{candidate.Rank}. {FormatMove(candidate.Move)}  {candidate.Score:+#;-#;0}"));
    }

    public static string FormatPrincipalVariation(IReadOnlyList<Move>? principalVariation)
    {
        if (principalVariation == null || principalVariation.Count == 0)
        {
            return "-";
        }

        return string.Join(Environment.NewLine,
            principalVariation.Take(12).Select((move, index) =>
                $"{index + 1}. {FormatMove(move)}"));
    }

    public static string FormatPrincipalVariationInline(IReadOnlyList<Move>? principalVariation)
    {
        if (principalVariation == null || principalVariation.Count == 0)
        {
            return "-";
        }

        return string.Join("  ", principalVariation.Take(8).Select(FormatMove));
    }

    public static string FormatMove(Move? move)
    {
        if (move == null)
        {
            return "-";
        }

        string captureText = move.CapturedPieces.Count > 0
            ? $" capture x{move.CapturedPieces.Count}"
            : string.Empty;

        return $"({move.Start.Item1},{move.Start.Item2}) → ({move.End.Item1},{move.End.Item2}){captureText}";
    }

    public static string FormatEvaluationBreakdown(EvaluationBreakdown breakdown)
    {
        return $"material {breakdown.Material}, pos {breakdown.Positioning}, adv {breakdown.Advancement}, mob {breakdown.Mobility}, cap {breakdown.CapturePotential}, vuln {breakdown.Vulnerability}, promo {breakdown.PromotionThreats}, prot {breakdown.Protection}";
    }
}
