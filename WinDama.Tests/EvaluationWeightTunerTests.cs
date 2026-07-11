using System.IO;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class EvaluationWeightTunerTests
{
    [Test]
    public void GenerateCandidates_CreatesBaseAndLocalVariants()
    {
        EvaluationProfile baseProfile = new EvaluationProfile("balanced", EvaluationWeights.Default);
        EvaluationWeightTuner tuner = new EvaluationWeightTuner();

        var candidates = tuner.GenerateCandidates(baseProfile, new EvaluationWeightTuningOptions
        {
            VariationPercent = 10,
            MaximumVariants = 5,
            WeightNames = new[] { nameof(EvaluationWeights.DamaValue), nameof(EvaluationWeights.PromotionThreatBonus) }
        });

        Assert.That(candidates.Count, Is.EqualTo(5));
        Assert.That(candidates[0].Name, Does.Contain("base"));
        Assert.That(candidates.Any(candidate => candidate.Name.Contains(nameof(EvaluationWeights.DamaValue))), Is.True);
        Assert.That(candidates.Select(candidate => candidate.Weights.DamaValue).Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void Tune_RanksGeneratedCandidates()
    {
        EvaluationProfile baseProfile = new EvaluationProfile("balanced", EvaluationWeights.Default);
        EvaluationWeightTuner tuner = new EvaluationWeightTuner();

        EvaluationWeightTuningSummary summary = tuner.Tune(TacticalPositionDirectory(), baseProfile, new EvaluationWeightTuningOptions
        {
            VariationPercent = 10,
            MaximumVariants = 3,
            WeightNames = new[] { nameof(EvaluationWeights.DamaValue) },
            BenchmarkOptions = new TacticalBenchmarkOptions
            {
                Depth = 1,
                ForcedCaptureComparisonDepth = 1,
                MaximumPositions = 1,
                RandomizeNearBestMoves = false
            }
        });

        Assert.That(summary.CandidateCount, Is.EqualTo(3));
        Assert.That(summary.BestResult, Is.Not.Null);
        Assert.That(summary.RankedResults, Is.Ordered.By(nameof(EvaluationWeightTuningResult.ScorePercent)).Descending);
    }

    [Test]
    public void SaveBestWeights_AndSaveCsv_CreateFiles()
    {
        EvaluationProfile baseProfile = new EvaluationProfile("balanced", EvaluationWeights.Default);
        EvaluationWeightTuner tuner = new EvaluationWeightTuner();

        EvaluationWeightTuningSummary summary = tuner.Tune(TacticalPositionDirectory(), baseProfile, new EvaluationWeightTuningOptions
        {
            MaximumVariants = 2,
            WeightNames = new[] { nameof(EvaluationWeights.PromotionThreatBonus) },
            BenchmarkOptions = new TacticalBenchmarkOptions
            {
                Depth = 1,
                ForcedCaptureComparisonDepth = 1,
                MaximumPositions = 1,
                RandomizeNearBestMoves = false
            }
        });

        string weightsPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "best-tuned-profile.json");
        string csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tuning-summary.csv");

        summary.SaveBestWeights(weightsPath);
        EvaluationWeightTuningSummary.SaveCsv(summary, csvPath);

        Assert.That(File.Exists(weightsPath), Is.True);
        Assert.That(File.Exists(csvPath), Is.True);
        Assert.That(File.ReadAllText(csvPath), Does.Contain("rank,candidate"));
        Assert.That(EvaluationWeights.Load(weightsPath).ManValue, Is.GreaterThan(0));
    }

    private static string TacticalPositionDirectory()
    {
        return LocateDirectory("TestPositions");
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
