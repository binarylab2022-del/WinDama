using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class LinearEvaluationTests
{
    [Test]
    public void FeatureExtractor_MaterialBalance_FavorsSideWithExtraMan()
    {
        int[,] board = EmptyBoard();
        board[5, 1] = 1;
        board[4, 2] = -1;
        board[6, 4] = 1;

        FeatureVector features = new LinearEvaluationFeatureExtractor().Extract(board, 1);

        Assert.That(features["man_balance"], Is.EqualTo(1));
        Assert.That(features["tempo"], Is.EqualTo(1));
    }

    [Test]
    public void LinearModel_SaveLoad_RoundTripsWeights()
    {
        LinearEvaluationModel model = new LinearEvaluationModel
        {
            Name = "unit-linear",
            Bias = 3.5,
            Weights =
            {
                ["man_balance"] = 100,
                ["dama_balance"] = 390
            }
        };

        string path = Path.Combine(Path.GetTempPath(), "windama-linear-" + Guid.NewGuid().ToString("N") + ".json");
        model.Save(path);
        LinearEvaluationModel loaded = LinearEvaluationModel.Load(path);

        Assert.That(loaded.Name, Is.EqualTo("unit-linear"));
        Assert.That(loaded.Bias, Is.EqualTo(3.5).Within(1e-9));
        Assert.That(loaded.Weights["man_balance"], Is.EqualTo(100).Within(1e-9));
    }

    [Test]
    public void LinearTrainer_LearnsMaterialPreferenceFromDataset()
    {
        PositionDataset dataset = new PositionDataset
        {
            Name = "synthetic-material",
            Samples =
            {
                Sample(BoardWithMaterial(ownMen: 2, opponentMen: 1), 1, 100),
                Sample(BoardWithMaterial(ownMen: 1, opponentMen: 2), 1, -100),
                Sample(BoardWithMaterial(ownMen: 3, opponentMen: 1), 1, 200),
                Sample(BoardWithMaterial(ownMen: 1, opponentMen: 3), 1, -200)
            }
        };

        LinearEvaluationTrainingResult result = new LinearEvaluationTrainer().Train(dataset, new LinearEvaluationTrainingOptions
        {
            ModelName = "synthetic-linear",
            L2Regularization = 0.01
        });

        Assert.That(result.SampleCount, Is.EqualTo(4));
        Assert.That(result.Model.Weights["man_balance"], Is.GreaterThan(0),
            "The learned material coefficient should favor the side with more men. Its absolute magnitude is not stable because correlated features and ridge regularization distribute the score across several weights.");

        LinearEvaluationFeatureExtractor extractor = new LinearEvaluationFeatureExtractor();
        double favorableScore = result.Model.Evaluate(extractor.Extract(BoardWithMaterial(ownMen: 3, opponentMen: 1), 1));
        double unfavorableScore = result.Model.Evaluate(extractor.Extract(BoardWithMaterial(ownMen: 1, opponentMen: 3), 1));

        Assert.That(favorableScore, Is.GreaterThan(unfavorableScore));
        Assert.That(Math.Sign(favorableScore), Is.EqualTo(1));
        Assert.That(Math.Sign(unfavorableScore), Is.EqualTo(-1));
        Assert.That(result.MeanAbsoluteError, Is.LessThan(80));
    }

    [Test]
    public void SearchEngine_CanUseLinearEvaluation()
    {
        LinearEvaluationModel model = LinearEvaluationModel.FromEvaluationWeights(EvaluationWeights.Default);
        SearchEngine engine = new SearchEngine(new MoveGenerator(), new LinearEvaluation(model), new Random(1));

        int[,] board = GameController.CreateInitialBoard();
        SearchResult result = engine.FindBestMove(board, 1, new SearchOptions
        {
            MaximumDepth = 1,
            UseQuiescenceSearch = false,
            UseTranspositionTable = false,
            UseKillerMoves = false,
            UseHistoryHeuristic = false
        });

        Assert.That(result.BestMove, Is.Not.Null);
        Assert.That(result.TopMoves, Is.Not.Empty);
    }

    private static PositionSample Sample(int[,] board, int player, int evaluation)
    {
        return new PositionSample
        {
            Source = "synthetic",
            PlayerToMove = player,
            Board = GameRecord.BoardToRows(board),
            Evaluation = evaluation,
            SearchDepth = 1
        };
    }

    private static int[,] EmptyBoard()
    {
        return new int[8, 8];
    }

    private static int[,] BoardWithMaterial(int ownMen, int opponentMen)
    {
        int[,] board = EmptyBoard();
        (int row, int col)[] ownSquares = { (7, 1), (6, 0), (6, 2), (5, 1) };
        (int row, int col)[] opponentSquares = { (0, 0), (1, 1), (1, 3), (2, 2) };

        for (int i = 0; i < ownMen; i++)
        {
            board[ownSquares[i].row, ownSquares[i].col] = 1;
        }

        for (int i = 0; i < opponentMen; i++)
        {
            board[opponentSquares[i].row, opponentSquares[i].col] = -1;
        }

        return board;
    }
}
