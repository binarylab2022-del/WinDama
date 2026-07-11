using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class TacticalPositionTests
{
    private readonly MoveGenerator _generator = new();

    [Test]
    public void BundledTacticalPositions_LoadAndValidate()
    {
        string directory = TacticalPositionDirectory();
        string[] files = Directory.GetFiles(directory, "*.json");

        Assert.That(files, Is.Not.Empty);

        foreach (string file in files)
        {
            TestPosition position = TestPositionStore.Load(file);
            Assert.That(position.ToBoard().GetLength(0), Is.EqualTo(8), Path.GetFileName(file));
            Assert.That(position.ToBoard().GetLength(1), Is.EqualTo(8), Path.GetFileName(file));
            Assert.That(position.CurrentPlayer, Is.AnyOf(1, -1), Path.GetFileName(file));
        }
    }

    [Test]
    public void MandatoryCaptureSample_AllQuietMovesAreSuppressed()
    {
        TestPosition position = Load("mandatory-capture-player1.json");
        int[,] board = position.ToBoard();

        var legalMoves = _generator.GetPlayerCapturesOrMoves(board, position.CurrentPlayer);

        Assert.That(legalMoves, Is.Not.Empty);
        Assert.That(legalMoves.All(IsCapture), Is.True);
        Assert.That(legalMoves.All(move => move.Start == (5, 1)), Is.True);
        Assert.That(legalMoves.Any(move => move.End == (3, 3) && move.CapturedPieces.Contains((4, 2))), Is.True);
    }

    [Test]
    public void LongestMultiCaptureSample_ReturnsOnlyLongestSequence()
    {
        TestPosition position = Load("longest-multicapture-player1.json");
        int[,] board = position.ToBoard();

        var legalMoves = _generator.GetPlayerCapturesOrMoves(board, position.CurrentPlayer);

        Assert.That(legalMoves, Is.Not.Empty);
        Assert.That(legalMoves.All(move => move.Start == (5, 1)), Is.True);
        Assert.That(legalMoves.All(move => move.CapturedPieces.Count == 2), Is.True);
        Assert.That(legalMoves.Any(move => move.CapturedPieces.Contains((4, 2)) && move.CapturedPieces.Contains((2, 4))), Is.True);
    }

    [Test]
    public void DamaLongCaptureSample_ReturnsFlyingCaptureLandings()
    {
        TestPosition position = Load("dama-long-capture-player1.json");
        int[,] board = position.ToBoard();

        var legalMoves = _generator.GetPlayerCapturesOrMoves(board, position.CurrentPlayer);

        Assert.That(legalMoves, Is.Not.Empty);
        Assert.That(legalMoves.All(IsCapture), Is.True);
        Assert.That(legalMoves.All(move => move.Start == (5, 1)), Is.True);
        Assert.That(legalMoves.All(move => move.CapturedPieces.Contains((3, 3))), Is.True);
        Assert.That(legalMoves.Any(move => move.End == (2, 4)), Is.True);
        Assert.That(legalMoves.Any(move => move.End == (1, 5)), Is.True);
        Assert.That(legalMoves.Any(move => move.End == (0, 6)), Is.True);
    }

    [Test]
    public void PromotionThreatSample_ApplyingPromotionMoveCreatesDama()
    {
        TestPosition position = Load("promotion-threat-player1.json");
        int[,] board = position.ToBoard();

        Move promotionMove = _generator
            .GetPlayerCapturesOrMoves(board, position.CurrentPlayer)
            .First(move => move.Start == (1, 2) && move.End.Item1 == 0);

        int[,] after = MoveExecutor.ApplyMoveForSearch(board, promotionMove, _generator);

        Assert.That(after[promotionMove.Start.Item1, promotionMove.Start.Item2], Is.EqualTo(0));
        Assert.That(after[promotionMove.End.Item1, promotionMove.End.Item2], Is.EqualTo(2));
    }

    [Test]
    public void BlockedSideSample_GameControllerDetectsWinForOpponent()
    {
        TestPosition position = Load("blocked-player2-no-moves.json");
        int[,] board = position.ToBoard();

        var controller = new GameController(board, position.CurrentPlayer, GameMode.HumanVsHuman, _generator);

        Assert.That(controller.GetCurrentLegalMoves(), Is.Empty);
        Assert.That(controller.IsGameOver, Is.True);
        Assert.That(controller.Status, Is.EqualTo(GameStatus.PlayerOneWon));
    }

    [Test]
    public void EqualLongestCapturesSample_SearchReportsBothCandidates()
    {
        TestPosition position = Load("two-equal-longest-captures-player1.json");
        int[,] board = position.ToBoard();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        SearchResult result = engine.FindBestMove(board, position.CurrentPlayer, new SearchOptions
        {
            Mode = SearchMode.FixedDepth,
            MaximumDepth = 4,
            Iterative = false,
            RandomizeNearBestMoves = false
        });

        Assert.That(result.IsForcedCaptureFastPath, Is.True);
        Assert.That(result.CaptureMoveCount, Is.EqualTo(2));
        Assert.That(result.TopMoves.Count, Is.EqualTo(2));
        Assert.That(result.TopMoves.All(candidate => candidate.Move.Start == (5, 3)), Is.True);
        Assert.That(result.TopMoves.All(candidate => candidate.Move.CapturedPieces.Count == 1), Is.True);
        Assert.That(result.TopMoves.Select(candidate => candidate.Move.End), Is.EquivalentTo(new[] { (3, 1), (3, 5) }));
    }

    [Test]
    public void DamaEndgameSample_SearchReturnsDamaCapture()
    {
        TestPosition position = Load("dama-endgame-capture-player1.json");
        int[,] board = position.ToBoard();
        var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

        SearchResult result = engine.FindBestMove(board, position.CurrentPlayer, SearchOptions.FixedDepth(3));

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.IsForcedCaptureFastPath, Is.True);
        Assert.That(result.BestMove!.Start, Is.EqualTo((4, 4)));
        Assert.That(result.BestMove.CapturedPieces, Does.Contain((2, 2)));
    }



[Test]
public void PromotionCaptureStopsSample_DoesNotContinueAsNewDama()
{
    TestPosition position = Load("promotion-capture-stops-player1.json");
    int[,] board = position.ToBoard();

    Move move = _generator.GetPlayerCapturesOrMoves(board, position.CurrentPlayer).Single();
    MoveExecutionResult result = MoveExecutor.ApplyMoveInPlace(board, move, _generator);

    Assert.That(board[0, 4], Is.EqualTo(2));
    Assert.That(result, Is.EqualTo(MoveExecutionResult.TurnShouldSwitch));
}

[Test]
public void DamaMultiCaptureSample_ReturnsTwoPieceSequence()
{
    TestPosition position = Load("dama-multicapture-player1.json");
    int[,] board = position.ToBoard();

    var legalMoves = _generator.GetPlayerCapturesOrMoves(board, position.CurrentPlayer);

    Assert.That(legalMoves, Is.Not.Empty);
    Assert.That(legalMoves.All(move => move.Start == (5, 1)), Is.True);
    Assert.That(legalMoves.All(move => move.CapturedPieces.Count == 2), Is.True);
    Assert.That(legalMoves.Any(move => move.End == (0, 6)), Is.True);
}

[Test]
public void LongerCaptureBeatsShorterSample_ReturnsOnlyLongerSequence()
{
    TestPosition position = Load("longer-capture-beats-shorter-player1.json");
    int[,] board = position.ToBoard();

    var legalMoves = _generator.GetPlayerCapturesOrMoves(board, position.CurrentPlayer);

    Assert.That(legalMoves, Is.Not.Empty);
    Assert.That(legalMoves.All(move => move.CapturedPieces.Count == 2), Is.True);
    Assert.That(legalMoves.All(move => move.Start == (5, 1)), Is.True);
}

[Test]
public void TripleCaptureSample_SearchKeepsMandatoryLongestSequence()
{
    TestPosition position = Load("triple-capture-player1.json");
    int[,] board = position.ToBoard();
    var engine = new SearchEngine(_generator, new Evaluation(), new Random(1));

    SearchResult result = engine.FindBestMove(board, position.CurrentPlayer, SearchOptions.FixedDepth(4));

    Assert.That(result.BestMove, Is.Not.Null);
    Assert.That(result.BestMove!.CapturedPieces.Count, Is.EqualTo(3));
    Assert.That(result.IsForcedCaptureFastPath, Is.True);
}

    private static TestPosition Load(string fileName)
    {
        return TestPositionStore.Load(Path.Combine(TacticalPositionDirectory(), fileName));
    }

    private static string TacticalPositionDirectory()
    {
        string directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestPositions");
        if (Directory.Exists(directory))
        {
            return directory;
        }

        // Fallback for IDEs that run tests without copying linked content.
        DirectoryInfo? current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "TestPositions");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the TestPositions directory.");
    }

    private static bool IsCapture(Move move)
    {
        return move.CapturedPieces.Count > 0;
    }
}
