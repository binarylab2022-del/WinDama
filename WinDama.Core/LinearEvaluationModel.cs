using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinDama.Core;

/// <summary>
/// Lightweight learned evaluator: score = bias + sum(weight_i * feature_i).
/// This is intentionally simple and transparent, making it a safe intermediate
/// step before neural or NNUE-style evaluators.
/// </summary>
public sealed class LinearEvaluationModel
{
    public string Name { get; init; } = "linear-evaluator";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public string TrainingSource { get; init; } = string.Empty;
    public int TrainingSampleCount { get; init; }
    public double Bias { get; init; }
    public Dictionary<string, double> Weights { get; init; } = new Dictionary<string, double>(StringComparer.Ordinal);

    [JsonIgnore]
    public IReadOnlyDictionary<string, double> ReadOnlyWeights => Weights;

    public int Evaluate(FeatureVector features)
    {
        if (features == null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        double raw = Bias + features.Dot(Weights);
        if (raw > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (raw < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    public string Explain(FeatureVector features, int maximumTerms = 8)
    {
        if (features == null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        IEnumerable<string> terms = features.Values
            .Select(pair => new
            {
                Name = pair.Key,
                Value = pair.Value,
                Weight = Weights.TryGetValue(pair.Key, out double w) ? w : 0.0,
                Contribution = pair.Value * (Weights.TryGetValue(pair.Key, out double w2) ? w2 : 0.0)
            })
            .Where(item => Math.Abs(item.Contribution) > 0.0001)
            .OrderByDescending(item => Math.Abs(item.Contribution))
            .Take(Math.Max(1, maximumTerms))
            .Select(item => $"{item.Name}={item.Contribution.ToString("+#;-#;0", CultureInfo.InvariantCulture)}");

        return string.Join(", ", terms);
    }

    public void Save(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A model file path is required.", nameof(filePath));
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions()));
    }

    public static LinearEvaluationModel Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A model file path is required.", nameof(filePath));
        }

        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<LinearEvaluationModel>(json, JsonOptions()) ?? new LinearEvaluationModel();
    }

    public static LinearEvaluationModel FromEvaluationWeights(EvaluationWeights evaluationWeights, string name = "linear-from-handcrafted")
    {
        if (evaluationWeights == null)
        {
            throw new ArgumentNullException(nameof(evaluationWeights));
        }

        return new LinearEvaluationModel
        {
            Name = name,
            TrainingSource = "handcrafted-weight-seed",
            Weights = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["man_balance"] = evaluationWeights.ManValue,
                ["dama_balance"] = evaluationWeights.DamaValue,
                ["advancement"] = evaluationWeights.AdvancedManWeight,
                ["center_control"] = evaluationWeights.CenterManBonus,
                ["edge_safety"] = evaluationWeights.EdgeSafetyBonus,
                ["back_row_guard"] = evaluationWeights.BackRowGuardBonus,
                ["mobility"] = evaluationWeights.MobilityWeight,
                ["dama_mobility"] = evaluationWeights.DamaMobilityWeight,
                ["capture_count"] = evaluationWeights.WinLikeCaptureSwing,
                ["best_capture_length"] = evaluationWeights.BestCaptureSequenceBonus,
                ["captured_material_available"] = evaluationWeights.WinLikeCaptureSwing,
                ["vulnerable_men"] = evaluationWeights.VulnerableManPenalty,
                ["vulnerable_damas"] = evaluationWeights.VulnerableDamaPenalty,
                ["protected_men"] = evaluationWeights.ProtectedManBonus,
                ["promotion_threats"] = evaluationWeights.PromotionThreatBonus,
                ["near_promotion"] = evaluationWeights.NearPromotionBonus,
                ["tempo"] = evaluationWeights.TempoBonus
            }
        };
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
