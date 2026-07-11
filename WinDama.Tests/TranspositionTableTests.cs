using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class TranspositionTableTests
{
    [Test]
    public void TranspositionTable_ExactEntry_ProbesAtEqualOrLowerDepth()
    {
        int[,] board = TestBoards.InitialPosition();
        var table = new TranspositionTable();
        ulong key = table.ComputeHash(board, currentPlayer: 1, rootPlayer: 1);
        var bestMove = new Move((5, 1), (4, 0));

        table.Store(key, depth: 4, score: 123, TranspositionBound.Exact, bestMove);

        bool hit = table.TryProbe(key, depth: 3, alpha: -1000, beta: 1000, out int score, out Move? storedMove);

        Assert.That(hit, Is.True);
        Assert.That(score, Is.EqualTo(123));
        Assert.That(storedMove, Is.Not.Null);
        Assert.That(storedMove!.Start, Is.EqualTo(bestMove.Start));
        Assert.That(storedMove.End, Is.EqualTo(bestMove.End));
        Assert.That(table.Hits, Is.EqualTo(1));
        Assert.That(table.Cutoffs, Is.EqualTo(1));
    }

    [Test]
    public void TranspositionTable_EntryTooShallow_DoesNotCutOffButReturnsBestMove()
    {
        int[,] board = TestBoards.InitialPosition();
        var table = new TranspositionTable();
        ulong key = table.ComputeHash(board, currentPlayer: 1, rootPlayer: 1);
        var bestMove = new Move((5, 1), (4, 0));

        table.Store(key, depth: 2, score: 123, TranspositionBound.Exact, bestMove);

        bool hit = table.TryProbe(key, depth: 4, alpha: -1000, beta: 1000, out _, out Move? storedMove);

        Assert.That(hit, Is.False);
        Assert.That(storedMove, Is.Not.Null);
        Assert.That(storedMove!.Start, Is.EqualTo(bestMove.Start));
        Assert.That(storedMove.End, Is.EqualTo(bestMove.End));
    }

    [Test]
    public void SearchEngine_WithTranspositionTable_StoresEntries()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(new MoveGenerator(), new Evaluation(), new System.Random(1));

        SearchResult result = engine.FindBestMove(board, 1, new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 3,
            Iterative = false,
            RandomizeNearBestMoves = false,
            UseTranspositionTable = true
        });

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.TranspositionStores, Is.GreaterThan(0));
        Assert.That(result.TranspositionEntries, Is.GreaterThan(0));
    }

    [Test]
    public void SearchEngine_CanDisableTranspositionTable()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(new MoveGenerator(), new Evaluation(), new System.Random(1));

        SearchResult result = engine.FindBestMove(board, 1, new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 3,
            Iterative = false,
            RandomizeNearBestMoves = false,
            UseTranspositionTable = false
        });

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.TranspositionStores, Is.EqualTo(0));
        Assert.That(result.TranspositionEntries, Is.EqualTo(0));
    }
}
