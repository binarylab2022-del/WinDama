using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Small, UI-independent notation helper used by analysis, benchmarks and tests.
/// Coordinates are displayed as 1-based row/column pairs to match the board
/// labels shown around the WPF board.
/// </summary>
public static class MoveNotation
{
    public static string Format(Move? move)
    {
        if (move == null)
        {
            return "-";
        }

        string separator = move.CapturedPieces.Count > 0 ? "x" : "-";
        string text = $"{Square(move.Start)}{separator}{Square(move.End)}";
        if (move.CapturedPieces.Count > 0)
        {
            text += $" ({move.CapturedPieces.Count})";
        }

        return text;
    }

    public static string FormatLine(IEnumerable<Move>? moves, int maximumMoves = 12)
    {
        if (moves == null)
        {
            return string.Empty;
        }

        List<string> parts = moves
            .Take(maximumMoves)
            .Select(Format)
            .ToList();

        return parts.Count == 0 ? string.Empty : string.Join(" ", parts);
    }

    public static string FormatTopMoves(IEnumerable<SearchCandidate>? candidates, int maximumMoves = 5)
    {
        if (candidates == null)
        {
            return string.Empty;
        }

        return string.Join(" | ", candidates
            .Take(maximumMoves)
            .Select(c => $"{c.Rank}. {Format(c.Move)} {c.Score:+#;-#;0}"));
    }

    private static string Square((int Row, int Column) square)
    {
        return $"{square.Row + 1},{square.Column + 1}";
    }
}
