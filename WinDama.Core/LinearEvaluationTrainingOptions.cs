namespace WinDama.Core;

public sealed class LinearEvaluationTrainingOptions
{
    public double L2Regularization { get; init; } = 0.001;
    public int MaximumSamples { get; init; } = 0;
    public string ModelName { get; init; } = "learned-linear-evaluator";
    public string TrainingSource { get; init; } = "position-dataset";
}
