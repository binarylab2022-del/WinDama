using System;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class SearchEngineTests
{
    private readonly MoveGenerator _generator = new();

    [Test]
    public void FindBestMove_FixedDepth_ReturnsLegalMove()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        SearchResult result = engine.FindBestMove(board, 1, SearchOptions.FixedDepth(2));
        var legalMoves = _generator.GetPlayerCapturesOrMoves(board, 1);

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(legalMoves.Any(move => SameMove(move, result.BestMove!)), Is.True);
        Assert.That(result.CompletedDepth, Is.EqualTo(2));
        Assert.That(result.Nodes, Is.GreaterThan(0));
        Assert.That(result.LegalMoveCount, Is.EqualTo(legalMoves.Count));
    }

    [Test]
    public void FindBestMove_DoesNotMutateInputBoard()
    {
        int[,] board = TestBoards.InitialPosition();
        int[,] before = (int[,])board.Clone();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        _ = engine.FindBestMove(board, 1, SearchOptions.FixedDepth(3));

        AssertBoardsEqual(before, board);
    }

    [Test]
    public void FindBestMove_WhenCaptureExists_ReturnsCapture()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[5, 5] = 1;
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        SearchResult result = engine.FindBestMove(board, 1, SearchOptions.FixedDepth(2));

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.BestMove!.CapturedPieces.Count, Is.GreaterThan(0));
    }

    [Test]
    public void FindBestMove_TimeLimitedSearch_ReturnsPartialResultWithoutThrowing()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedTimePerMove,
            MaximumDepth = 64,
            TimeLimitMilliseconds = 100,
            Iterative = true,
            TieBreakTolerance = 10
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.LegalMoveCount, Is.GreaterThan(0));
        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.CompletedDepth, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.TimeBudgetMilliseconds, Is.EqualTo(100));
    }


    [Test]
    public void FindBestMove_WhenLongerCaptureExists_IgnoresShorterCapturesAndQuietMoves()
    {
        int[,] board = TestBoards.Empty();

        // Longest legal sequence starts from (5,1) and captures two pieces.
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[2, 4] = -1;

        // This piece has only a one-capture sequence and must be ignored.
        board[5, 5] = 1;
        board[4, 6] = -1;

        // This piece has only quiet moves and must also be ignored.
        board[6, 0] = 1;

        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        SearchResult result = engine.FindBestMove(board, 1, SearchOptions.FixedDepth(2));

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.BestMove!.Start, Is.EqualTo((5, 1)));
        Assert.That(result.BestMove!.CapturedPieces.Count, Is.EqualTo(2));
    }



    [Test]
    public void FindBestMove_WhenCaptureExists_SkipsDepthAndTimeBudget()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedTimePerMove,
            MaximumDepth = 64,
            TimeLimitMilliseconds = 5000,
            Iterative = true
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.BestMove!.CapturedPieces.Count, Is.EqualTo(1));
        Assert.That(result.IsForcedCaptureFastPath, Is.True);
        Assert.That(result.CompletedDepth, Is.EqualTo(0));
        Assert.That(result.TimeBudgetMilliseconds, Is.EqualTo(0));
        Assert.That(result.CaptureMoveCount, Is.GreaterThan(0));
        Assert.That(result.StopReason, Does.Contain("Mandatory"));
    }

    [Test]
    public void FindBestMove_ReportsSearchProgress()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));
        int progressCalls = 0;
        SearchProgress? lastProgress = null;
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 2,
            Iterative = false,
            ProgressIntervalMilliseconds = 1,
            Progress = progress =>
            {
                progressCalls++;
                lastProgress = progress;
            }
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(progressCalls, Is.GreaterThan(0));
        Assert.That(lastProgress, Is.Not.Null);
        Assert.That(lastProgress!.LegalMoveCount, Is.EqualTo(result.LegalMoveCount));
        Assert.That(lastProgress.Nodes, Is.EqualTo(result.Nodes));
    }


    [Test]
    public void FindBestMove_ReturnsTopCandidateMoves()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        var options = new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 2,
            Iterative = false,
            RandomizeNearBestMoves = false
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.TopMoves, Is.Not.Null);
        Assert.That(result.TopMoves.Count, Is.GreaterThan(0));
        Assert.That(result.TopMoves.Count, Is.LessThanOrEqualTo(5));
        Assert.That(SameMove(result.TopMoves[0].Move, result.BestMove!), Is.True);
        Assert.That(result.TopMoves.Select(candidate => candidate.Rank), Is.EqualTo(Enumerable.Range(1, result.TopMoves.Count)));
    }

    [Test]
    public void FindBestMove_ProgressReportsTopCandidateMoves()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));
        SearchProgress? lastProgress = null;
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 2,
            Iterative = false,
            ProgressIntervalMilliseconds = 1,
            Progress = progress => lastProgress = progress
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(lastProgress, Is.Not.Null);
        Assert.That(lastProgress!.TopMoves.Count, Is.EqualTo(result.TopMoves.Count));
        Assert.That(SameMove(lastProgress.TopMoves.First().Move, result.TopMoves.First().Move), Is.True);
    }


    [Test]
    public void FindBestMove_ReturnsPrincipalVariation()
    {
        int[,] board = TestBoards.InitialPosition();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        SearchResult result = engine.FindBestMove(board, 1, SearchOptions.FixedDepth(2));

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.PrincipalVariation, Is.Not.Empty);
        Assert.That(SameMove(result.PrincipalVariation[0], result.BestMove!), Is.True);
    }

    [Test]
    public void FindBestMove_MultipleMandatoryCaptures_ComparesCapturesWithoutTimeBudget()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[5, 5] = 1;
        board[4, 6] = -1;

        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedTimePerMove,
            MaximumDepth = 64,
            TimeLimitMilliseconds = 5000,
            Iterative = true,
            ForcedCaptureComparisonDepth = 3,
            RandomizeNearBestMoves = false
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.CaptureMoveCount, Is.EqualTo(2));
        Assert.That(result.IsForcedCaptureFastPath, Is.True);
        Assert.That(result.CompletedDepth, Is.EqualTo(3));
        Assert.That(result.TimeBudgetMilliseconds, Is.EqualTo(0));
        Assert.That(result.StopReason, Does.Contain("Multiple"));
        Assert.That(result.PrincipalVariation, Is.Not.Empty);
        Assert.That(result.Nodes, Is.GreaterThan(result.CaptureMoveCount));
    }


    [Test]
    public void FindBestMove_QuiescenceSearch_ExtendsLeafCaptures()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[2, 4] = -1;

        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 1,
            Iterative = false,
            UseQuiescenceSearch = true,
            MaxQuiescenceDepth = 4,
            RandomizeNearBestMoves = false
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.UsedQuiescenceSearch, Is.True);
        Assert.That(result.QuiescenceNodes, Is.GreaterThan(0));
        Assert.That(result.Nodes, Is.GreaterThan(result.QuiescenceNodes));
    }

    [Test]
    public void FindBestMove_QuiescenceSearch_CanBeDisabled()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[2, 4] = -1;

        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));
        var options = new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 1,
            Iterative = false,
            UseQuiescenceSearch = false,
            RandomizeNearBestMoves = false
        };

        SearchResult result = engine.FindBestMove(board, 1, options);

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.UsedQuiescenceSearch, Is.False);
        Assert.That(result.QuiescenceNodes, Is.EqualTo(0));
    }

    private static bool SameMove(Move left, Move right)    {
        return left.Start == right.Start
            && left.End == right.End
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }

    private static void AssertBoardsEqual(int[,] expected, int[,] actual)
    {
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                Assert.That(actual[row, column], Is.EqualTo(expected[row, column]), $"Mismatch at ({row},{column})");
            }
        }
    }
}
