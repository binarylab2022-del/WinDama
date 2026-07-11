using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class PositionDatasetTests
{
    [Test]
    public void FromSearchResult_CreatesSampleWithPvAndTopMoves()
    {
        int[,] board = GameController.CreateInitialBoard();
        SearchEngine engine = new SearchEngine();
        SearchResult result = engine.FindBestMove(board, 1, SearchOptions.FixedDepth(2));

        PositionDatasetExporter exporter = new PositionDatasetExporter();
        PositionSample sample = exporter.FromSearchResult(board, 1, result, "balanced-default", "opponent");

        Assert.That(sample.PlayerToMove, Is.EqualTo(1));
        Assert.That(sample.BestMove, Is.Not.Empty);
        Assert.That(sample.SearchDepth, Is.GreaterThanOrEqualTo(1));
        Assert.That(sample.TopMoves, Is.Not.Empty);
        Assert.That(sample.Board.Length, Is.EqualTo(8));
        Assert.That(sample.PlayerProfile, Is.EqualTo("balanced-default"));
    }

    [Test]
    public void FromGameDatabase_ReplaysTournamentGamesIntoSamples()
    {
        EvaluationProfile profileA = new EvaluationProfile("profile-a", EvaluationWeights.Default);
        EvaluationProfile profileB = new EvaluationProfile("profile-b", EvaluationWeights.Default);
        EvaluationTournamentRunner runner = new EvaluationTournamentRunner();
        EvaluationTournamentSummary summary = runner.Run(profileA, profileB, new EvaluationTournamentOptions
        {
            Depth = 1,
            GamesPerColor = 1,
            MaxPliesPerGame = 8,
            UseQuiescenceSearch = false,
            UseTranspositionTable = false,
            UseKillerMoves = false,
            UseHistoryHeuristic = false
        });

        GameDatabase database = GameDatabase.FromTournament(summary);
        PositionDataset dataset = new PositionDatasetExporter().FromGameDatabase(database);

        Assert.That(dataset.SampleCount, Is.EqualTo(database.TotalPlies));
        Assert.That(dataset.Samples.All(sample => !string.IsNullOrWhiteSpace(sample.BestMove)), Is.True);
        Assert.That(dataset.Samples.Select(sample => sample.SourceGameId).Distinct().Count(), Is.EqualTo(database.GameCount));
    }

    [Test]
    public void PositionDataset_SaveJsonLinesCsvAndJson_CreatesFiles()
    {
        int[,] board = GameController.CreateInitialBoard();
        SearchResult result = new SearchEngine().FindBestMove(board, 1, SearchOptions.FixedDepth(1));
        PositionSample sample = PositionSample.FromSearchResult(board, 1, result, "unit-test");
        PositionDataset dataset = new PositionDataset
        {
            Name = "unit-test-dataset",
            Samples = { sample }
        };

        string directory = Path.Combine(Path.GetTempPath(), "windama-dataset-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string jsonl = Path.Combine(directory, "positions.jsonl");
        string csv = Path.Combine(directory, "positions.csv");
        string json = Path.Combine(directory, "positions.json");

        dataset.SaveJsonLines(jsonl);
        dataset.SaveCsv(csv);
        dataset.SaveJson(json);

        Assert.That(File.Exists(jsonl), Is.True);
        Assert.That(File.ReadAllLines(jsonl), Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(csv), Does.Contain("sample_id,source"));
        Assert.That(PositionDataset.LoadJson(json).SampleCount, Is.EqualTo(1));
    }
}
