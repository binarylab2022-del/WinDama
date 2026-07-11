using System;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class SearchHeuristicTests
{
    [Test]
    public void SearchOptions_EnableKillerMovesAndHistoryByDefault()
    {
        SearchOptions options = SearchOptions.FixedDepth(3);

        Assert.That(options.UseKillerMoves, Is.True);
        Assert.That(options.UseHistoryHeuristic, Is.True);
        Assert.That(options.KillerMovesPerDepth, Is.EqualTo(2));
    }

    [Test]
    public void SearchEngine_WhenOrderingHeuristicsDisabled_ReportsNoHeuristicUpdates()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(new MoveGenerator(), new Evaluation(), new Random(1));
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 4,
            Iterative = false,
            UseKillerMoves = false,
            UseHistoryHeuristic = false
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.KillerMoveCutoffs, Is.EqualTo(0));
        Assert.That(result.HistoryHeuristicUpdates, Is.EqualTo(0));
    }

    [Test]
    public void SearchEngine_ReportsHeuristicCountersWithoutChangingLegality()
    {
        int[,] board = TestBoards.InitialPosition();
        var generator = new MoveGenerator();
        var engine = new SearchEngine(generator, new Evaluation(), new Random(1));
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 4,
            Iterative = false,
            UseKillerMoves = true,
            UseHistoryHeuristic = true
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(generator.GetPlayerCapturesOrMoves(board, 1).Any(move => SameMove(move, result.BestMove!)), Is.True);
        Assert.That(result.KillerMoveCutoffs, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.HistoryHeuristicUpdates, Is.GreaterThanOrEqualTo(0));
    }

    private static bool SameMove(Move left, Move right)
    {
        return left.Start == right.Start
            && left.End == right.End
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }
}
