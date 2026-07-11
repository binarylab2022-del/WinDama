using System;

namespace WinDama.Core;

/// <summary>
/// Named evaluation configuration used for benchmarking, tuning, and tournaments.
/// It can represent either a handcrafted weight profile or a learned linear model.
/// </summary>
public sealed class EvaluationProfile
{
    public EvaluationProfile(string name, EvaluationWeights weights, string? sourcePath = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unnamed profile" : name.Trim();
        Weights = weights ?? EvaluationWeights.Default;
        SourcePath = sourcePath;
    }

    public EvaluationProfile(string name, LinearEvaluationModel linearModel, string? sourcePath = null)
    {
        if (linearModel == null)
        {
            throw new ArgumentNullException(nameof(linearModel));
        }

        Name = string.IsNullOrWhiteSpace(name) ? linearModel.Name : name.Trim();
        Weights = EvaluationWeights.Default;
        LinearModel = linearModel;
        SourcePath = sourcePath;
    }

    public string Name { get; }
    public EvaluationWeights Weights { get; }
    public LinearEvaluationModel? LinearModel { get; }
    public string? SourcePath { get; }
    public bool IsLearned => LinearModel != null;
    public string EvaluatorType => IsLearned ? "linear" : "handcrafted";

    public Evaluation CreateEvaluation()
    {
        return LinearModel != null
            ? new LinearEvaluation(LinearModel)
            : new Evaluation(Weights);
    }

    public override string ToString()
    {
        return IsLearned ? $"{Name} [linear]" : Name;
    }
}
