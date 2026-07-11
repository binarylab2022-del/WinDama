using System.IO;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class TacticalBenchmarkTests
{
    [Test]
    public void EvaluationProfileStore_LoadsBundledProfiles()
    {
        var profiles = EvaluationProfileStore.LoadProfiles(EvaluationProfileDirectory());

        Assert.That(profiles.Count, Is.GreaterThanOrEqualTo(4));
        Assert.That(profiles.Any(profile => profile.Name.Contains("balanced")), Is.True);
        Assert.That(profiles.All(profile => profile.Weights.ManValue > 0), Is.True);
    }

    [Test]
    public void TacticalBenchmarkRunner_ProducesProfilePositionMatrix()
    {
        string positions = TacticalPositionDirectory();
        string profiles = EvaluationProfileDirectory();
        int positionCount = Directory.GetFiles(positions, "*.json").Length;
        int profileCount = Directory.GetFiles(profiles, "*.json").Length;

        var runner = new TacticalBenchmarkRunner();
        TacticalBenchmarkSummary summary = runner.Run(positions, profiles, new TacticalBenchmarkOptions
        {
            Depth = 2,
            ForcedCaptureComparisonDepth = 2,
            MaximumPositions = 3,
            RandomizeNearBestMoves = false
        });

        Assert.That(positionCount, Is.GreaterThanOrEqualTo(3));
        Assert.That(profileCount, Is.GreaterThanOrEqualTo(4));
        Assert.That(summary.ProfileCount, Is.EqualTo(profileCount));
        Assert.That(summary.PositionCount, Is.EqualTo(3));
        Assert.That(summary.ResultCount, Is.EqualTo(3 * profileCount));
        Assert.That(summary.Results.All(result => !string.IsNullOrWhiteSpace(result.ProfileName)), Is.True);
        Assert.That(summary.Results.All(result => !string.IsNullOrWhiteSpace(result.PositionFile)), Is.True);
        Assert.That(summary.Results.All(result => !string.IsNullOrWhiteSpace(result.BestMove)), Is.True);
    }

    [Test]
    public void TacticalBenchmarkRunner_SaveCsv_CreatesReadableFile()
    {
        var runner = new TacticalBenchmarkRunner();
        TacticalBenchmarkSummary summary = runner.Run(TacticalPositionDirectory(), EvaluationProfileDirectory(), new TacticalBenchmarkOptions
        {
            Depth = 1,
            ForcedCaptureComparisonDepth = 1,
            MaximumPositions = 1,
            RandomizeNearBestMoves = false
        });

        string outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tactical-benchmark.csv");
        TacticalBenchmarkRunner.SaveCsv(summary, outputPath);

        Assert.That(File.Exists(outputPath), Is.True);
        string csv = File.ReadAllText(outputPath);
        Assert.That(csv, Does.Contain("profile,evaluator_type,position"));
        Assert.That(csv, Does.Contain("best_move"));
    }

    [Test]
    public void DifferentEvaluationProfiles_CanProduceDifferentRawEvaluations()
    {
        string profilesDirectory = EvaluationProfileDirectory();
        var profiles = EvaluationProfileStore.LoadProfiles(profilesDirectory);
        EvaluationProfile defensive = profiles.First(profile => profile.Name.Contains("defensive"));
        EvaluationProfile aggressive = profiles.First(profile => profile.Name.Contains("aggressive"));

        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[2, 4] = -2;

        int defensiveScore = new Evaluation(defensive.Weights).EvaluateBoard(board, 1);
        int aggressiveScore = new Evaluation(aggressive.Weights).EvaluateBoard(board, 1);

        Assert.That(defensiveScore, Is.Not.EqualTo(aggressiveScore));
    }



    [Test]
    public void TacticalBenchmarkRunner_ScoresProfilesUsingExpectedFields()
    {
        var runner = new TacticalBenchmarkRunner();
        TacticalBenchmarkSummary summary = runner.Run(TacticalPositionDirectory(), EvaluationProfileDirectory(), new TacticalBenchmarkOptions
        {
            Depth = 2,
            ForcedCaptureComparisonDepth = 2,
            MaximumPositions = 2,
            RandomizeNearBestMoves = false
        });

        Assert.That(summary.Results, Has.Some.Matches<TacticalBenchmarkPositionResult>(result => result.MaximumScore > 0));
        Assert.That(summary.Profiles.All(profile => profile.MaximumScore > 0), Is.True);
        Assert.That(summary.Profiles, Is.Ordered.By(nameof(TacticalBenchmarkProfileSummary.ScorePercent)).Descending);
    }

    [Test]
    public void TacticalBenchmarkRunner_CsvIncludesScoringColumns()
    {
        var runner = new TacticalBenchmarkRunner();
        TacticalBenchmarkSummary summary = runner.Run(TacticalPositionDirectory(), EvaluationProfileDirectory(), new TacticalBenchmarkOptions
        {
            Depth = 1,
            ForcedCaptureComparisonDepth = 1,
            MaximumPositions = 1,
            RandomizeNearBestMoves = false
        });

        string outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tactical-benchmark-scored.csv");
        TacticalBenchmarkRunner.SaveCsv(summary, outputPath);

        string csv = File.ReadAllText(outputPath);
        Assert.That(csv, Does.Contain("score,max_score,score_percent"));
        Assert.That(csv, Does.Contain("expected_best_move"));
    }

    [Test]
    public void TacticalBenchmarkRunner_CanCompareHandcraftedAndLinearProfiles()
    {
        EvaluationProfile handcrafted = new EvaluationProfile("handcrafted-default", EvaluationWeights.Default);
        LinearEvaluationModel model = LinearEvaluationModel.FromEvaluationWeights(EvaluationWeights.Default, "linear-seed");
        EvaluationProfile learned = new EvaluationProfile("learned-linear", model);

        var runner = new TacticalBenchmarkRunner();
        TacticalBenchmarkSummary summary = runner.Run(TacticalPositionDirectory(), new[] { handcrafted, learned }, new TacticalBenchmarkOptions
        {
            Depth = 1,
            ForcedCaptureComparisonDepth = 1,
            MaximumPositions = 1,
            UseQuiescenceSearch = false,
            RandomizeNearBestMoves = false
        });

        Assert.That(summary.ProfileCount, Is.EqualTo(2));
        Assert.That(summary.Results, Has.Some.Matches<TacticalBenchmarkPositionResult>(result => result.ProfileName == "learned-linear"));
        Assert.That(summary.Results.First(result => result.ProfileName == "learned-linear").EvaluatorType, Is.EqualTo("linear"));
        Assert.That(summary.Results.All(result => !string.IsNullOrWhiteSpace(result.BestMove)), Is.True);
    }

    private static string TacticalPositionDirectory()
    {
        return LocateDirectory("TestPositions");
    }

    private static string EvaluationProfileDirectory()
    {
        return LocateDirectory("EvaluationWeights");
    }

    private static string LocateDirectory(string directoryName)
    {
        string direct = Path.Combine(TestContext.CurrentContext.TestDirectory, directoryName);
        if (Directory.Exists(direct))
        {
            return direct;
        }

        DirectoryInfo? current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, directoryName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Unable to locate {directoryName}.");
    }
}
