using System.IO;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class OpeningBookTests
{
    [Test]
    public void GameDatabase_SaveLoad_PreservesRecordedMoves()
    {
        MoveGenerator generator = new MoveGenerator();
        int[,] board = GameController.CreateInitialBoard();
        Move move = generator.GetPlayerCapturesOrMoves(board, 1).First();
        GameDatabase database = new GameDatabase();
        database.Add(new GameRecord
        {
            PlayerOneProfile = "A",
            PlayerTwoProfile = "B",
            Winner = 1,
            ResultText = "1-0",
            Moves = { GameMoveRecord.FromMove(1, 1, move, 12, 1) }
        });

        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "opening-db-test.json");
        database.Save(path);
        GameDatabase loaded = GameDatabase.Load(path);

        Assert.That(loaded.GameCount, Is.EqualTo(1));
        Assert.That(loaded.Games[0].Moves, Has.Count.EqualTo(1));
        Assert.That(loaded.Games[0].Moves[0].Notation, Is.EqualTo(MoveNotation.Format(move)));
    }

    [Test]
    public void OpeningBook_Build_RecommendsLegalInitialMove()
    {
        MoveGenerator generator = new MoveGenerator();
        int[,] board = GameController.CreateInitialBoard();
        Move move = generator.GetPlayerCapturesOrMoves(board, 1).First();
        GameDatabase database = new GameDatabase();
        database.Add(new GameRecord
        {
            PlayerOneProfile = "A",
            PlayerTwoProfile = "B",
            Winner = 1,
            ResultText = "1-0",
            Moves = { GameMoveRecord.FromMove(1, 1, move, 30, 2) }
        });

        OpeningBook book = OpeningBook.Build(database, maxBookPlies: 8);
        OpeningBookRecommendation? recommendation = book.RecommendMove(board, 1, generator.GetPlayerCapturesOrMoves(board, 1));

        Assert.That(book.EntryCount, Is.EqualTo(1));
        Assert.That(recommendation, Is.Not.Null);
        Assert.That(MoveNotation.Format(recommendation!.Move), Is.EqualTo(MoveNotation.Format(move)));
        Assert.That(recommendation.Entry.ScorePercent, Is.EqualTo(100.0).Within(0.001));
    }

    [Test]
    public void EvaluationTournament_ProducesMoveRecords_ForGameDatabase()
    {
        EvaluationProfile profile = new EvaluationProfile("test-profile", EvaluationWeights.Default);
        EvaluationTournamentRunner runner = new EvaluationTournamentRunner();
        EvaluationTournamentSummary summary = runner.Run(profile, profile, new EvaluationTournamentOptions
        {
            Depth = 1,
            GamesPerColor = 1,
            MaxPliesPerGame = 4,
            MaxMovesWithoutCaptureOrPromotion = 4,
            UseQuiescenceSearch = false,
            UseTranspositionTable = false,
            UseKillerMoves = false,
            UseHistoryHeuristic = false
        });

        GameDatabase database = GameDatabase.FromTournament(summary);
        OpeningBook book = OpeningBook.Build(database, maxBookPlies: 4);

        Assert.That(summary.Games, Is.Not.Empty);
        Assert.That(summary.Games[0].MoveRecords, Is.Not.Empty);
        Assert.That(database.GameCount, Is.EqualTo(summary.GameCount));
        Assert.That(book.EntryCount, Is.GreaterThan(0));
    }
}
