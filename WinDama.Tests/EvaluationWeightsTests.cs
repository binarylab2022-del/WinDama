using System.IO;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class EvaluationWeightsTests
{
    [Test]
    public void CustomDamaValue_ChangesMaterialEvaluation()
    {
        int[,] board = TestBoards.Empty();
        board[3, 3] = 2;

        int defaultScore = new Evaluation().EvaluateBreakdown(board, 1).Material;
        int tunedScore = new Evaluation(new EvaluationWeights { DamaValue = 500 }).EvaluateBreakdown(board, 1).Material;

        Assert.That(defaultScore, Is.EqualTo(EvaluationWeights.Default.DamaValue));
        Assert.That(tunedScore, Is.EqualTo(500));
    }

    [Test]
    public void EvaluationWeights_SaveAndLoad_RoundTripsValues()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "eval-weights-test.json");
        EvaluationWeights weights = new EvaluationWeights
        {
            ManValue = 110,
            DamaValue = 430,
            MobilityWeight = 5,
            VulnerableManPenalty = 70
        };

        weights.Save(path);
        EvaluationWeights loaded = EvaluationWeights.Load(path);

        Assert.That(loaded.ManValue, Is.EqualTo(110));
        Assert.That(loaded.DamaValue, Is.EqualTo(430));
        Assert.That(loaded.MobilityWeight, Is.EqualTo(5));
        Assert.That(loaded.VulnerableManPenalty, Is.EqualTo(70));
    }
}
