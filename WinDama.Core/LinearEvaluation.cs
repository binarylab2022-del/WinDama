using System;

namespace WinDama.Core;

/// <summary>
/// Evaluation adapter that lets SearchEngine use a learned linear model through
/// the same Evaluation abstraction used by the handcrafted evaluator.
/// </summary>
public sealed class LinearEvaluation : Evaluation
{
    private readonly LinearEvaluationModel model;
    private readonly LinearEvaluationFeatureExtractor featureExtractor;

    public LinearEvaluation(LinearEvaluationModel model)
        : this(model, new LinearEvaluationFeatureExtractor())
    {
    }

    public LinearEvaluation(LinearEvaluationModel model, LinearEvaluationFeatureExtractor featureExtractor)
        : base(EvaluationWeights.Default)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        this.featureExtractor = featureExtractor ?? throw new ArgumentNullException(nameof(featureExtractor));
    }

    public LinearEvaluationModel Model => model;

    public override int EvaluateBoard(int[,] board, int currentPlayer)
    {
        FeatureVector features = featureExtractor.Extract(board, currentPlayer);
        return model.Evaluate(features);
    }
}
