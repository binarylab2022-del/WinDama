using System;
using System.Collections.Generic;

namespace WinDama.Core;

public sealed class LinearEvaluationTrainingResult
{
    public LinearEvaluationModel Model { get; init; } = new LinearEvaluationModel();
    public int SampleCount { get; init; }
    public double MeanAbsoluteError { get; init; }
    public double RootMeanSquaredError { get; init; }
    public Dictionary<string, double> FeatureWeights => Model.Weights;

    public string Summary => $"{Model.Name}: samples {SampleCount}, MAE {MeanAbsoluteError:0.0}, RMSE {RootMeanSquaredError:0.0}";
}
