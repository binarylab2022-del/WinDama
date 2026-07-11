using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Compares one engine result with optional expectations embedded in a
/// TestPosition. Missing expectations are ignored, so old position files remain
/// valid but simply contribute fewer scoring points.
/// </summary>
public static class TacticalBenchmarkScoring
{
    public static TacticalBenchmarkScore Score(TestPosition position, SearchResult searchResult)
    {
        if (position == null)
        {
            throw new ArgumentNullException(nameof(position));
        }

        if (searchResult == null)
        {
            throw new ArgumentNullException(nameof(searchResult));
        }

        int score = 0;
        int maximum = 0;
        List<string> details = new();

        string bestMove = MoveNotation.Format(searchResult.BestMove);
        string[] expectedBestMoves = ExpectedBestMoves(position).ToArray();
        if (expectedBestMoves.Length > 0)
        {
            maximum += 3;
            bool ok = ContainsMove(expectedBestMoves, bestMove);
            if (ok)
            {
                score += 3;
            }

            details.Add(ok ? "best=ok" : $"best=fail expected {string.Join(" / ", expectedBestMoves)} got {bestMove}");
        }

        string[] expectedPvFirstMoves = ExpectedPvFirstMoves(position, expectedBestMoves).ToArray();
        if (expectedPvFirstMoves.Length > 0)
        {
            maximum += 1;
            string pvFirst = searchResult.PrincipalVariation.Count > 0
                ? MoveNotation.Format(searchResult.PrincipalVariation[0])
                : bestMove;
            bool ok = ContainsMove(expectedPvFirstMoves, pvFirst);
            if (ok)
            {
                score += 1;
            }

            details.Add(ok ? "pv1=ok" : $"pv1=fail expected {string.Join(" / ", expectedPvFirstMoves)} got {pvFirst}");
        }

        if (!string.IsNullOrWhiteSpace(position.ExpectedEvaluationSign) && position.ExpectedEvaluationSign != "any")
        {
            maximum += 1;
            bool ok = EvaluationSignMatches(position.ExpectedEvaluationSign, searchResult.BestEvaluation);
            if (ok)
            {
                score += 1;
            }

            details.Add(ok ? "sign=ok" : $"sign=fail expected {position.ExpectedEvaluationSign} got {searchResult.BestEvaluation}");
        }

        if (position.ExpectedLegalMoveCount.HasValue)
        {
            maximum += 1;
            bool ok = searchResult.LegalMoveCount == position.ExpectedLegalMoveCount.Value;
            if (ok)
            {
                score += 1;
            }

            details.Add(ok ? "legal=ok" : $"legal=fail expected {position.ExpectedLegalMoveCount.Value} got {searchResult.LegalMoveCount}");
        }

        if (position.ExpectedCaptureMoveCount.HasValue)
        {
            maximum += 1;
            bool ok = searchResult.CaptureMoveCount == position.ExpectedCaptureMoveCount.Value;
            if (ok)
            {
                score += 1;
            }

            details.Add(ok ? "captures=ok" : $"captures=fail expected {position.ExpectedCaptureMoveCount.Value} got {searchResult.CaptureMoveCount}");
        }

        if (position.ExpectedMinBestMoveCaptureCount.HasValue)
        {
            maximum += 1;
            int actual = searchResult.BestMove?.CapturedPieces.Count ?? 0;
            bool ok = actual >= position.ExpectedMinBestMoveCaptureCount.Value;
            if (ok)
            {
                score += 1;
            }

            details.Add(ok ? "bestCaps=ok" : $"bestCaps=fail expected >= {position.ExpectedMinBestMoveCaptureCount.Value} got {actual}");
        }

        return new TacticalBenchmarkScore(score, maximum, details);
    }

    private static IEnumerable<string> ExpectedBestMoves(TestPosition position)
    {
        if (!string.IsNullOrWhiteSpace(position.ExpectedBestMove))
        {
            yield return position.ExpectedBestMove.Trim();
        }

        foreach (string move in position.ExpectedBestMoves ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(move))
            {
                yield return move.Trim();
            }
        }
    }

    private static IEnumerable<string> ExpectedPvFirstMoves(TestPosition position, string[] expectedBestMoves)
    {
        if (position.ExpectedPvFirstMoves != null && position.ExpectedPvFirstMoves.Length > 0)
        {
            foreach (string move in position.ExpectedPvFirstMoves)
            {
                if (!string.IsNullOrWhiteSpace(move))
                {
                    yield return move.Trim();
                }
            }

            yield break;
        }

        foreach (string move in expectedBestMoves)
        {
            yield return move;
        }
    }

    private static bool ContainsMove(IEnumerable<string> expectedMoves, string actualMove)
    {
        string actual = NormalizeMove(actualMove);
        return expectedMoves.Any(expected => NormalizeMove(expected) == actual);
    }

    private static string NormalizeMove(string? move)
    {
        return (move ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static bool EvaluationSignMatches(string expected, int evaluation)
    {
        return expected.Trim().ToLowerInvariant() switch
        {
            "positive" or "+" or ">0" => evaluation > 0,
            "negative" or "-" or "<0" => evaluation < 0,
            "zero" or "0" => evaluation == 0,
            "nonnegative" or ">=0" => evaluation >= 0,
            "nonpositive" or "<=0" => evaluation <= 0,
            _ => true
        };
    }
}

public sealed class TacticalBenchmarkScore
{
    public TacticalBenchmarkScore(int score, int maximumScore, IReadOnlyList<string> details)
    {
        Score = score;
        MaximumScore = maximumScore;
        Details = details;
    }

    public int Score { get; }
    public int MaximumScore { get; }
    public bool HasExpectations => MaximumScore > 0;
    public IReadOnlyList<string> Details { get; }
    public string DetailsText => string.Join("; ", Details);
}
