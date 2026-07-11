using System;
using System.IO;
using System.Text.Json;

namespace WinDama.Core;

/// <summary>
/// Tunable weights used by the handcrafted evaluation function.
///
/// Keeping the weights outside Evaluation.cs makes the engine easier to tune,
/// benchmark, and later replace with a learned evaluator without changing the
/// search code.
/// </summary>
public sealed class EvaluationWeights
{
    public int ManValue { get; init; } = 100;
    public int DamaValue { get; init; } = 390;
    public int WinLikeCaptureSwing { get; init; } = 60;
    public int MobilityWeight { get; init; } = 3;
    public int DamaMobilityWeight { get; init; } = 4;
    public int CenterManBonus { get; init; } = 6;
    public int CenterDamaBonus { get; init; } = 14;
    public int EdgeSafetyBonus { get; init; } = 4;
    public int BackRowGuardBonus { get; init; } = 8;
    public int AdvancedManWeight { get; init; } = 7;
    public int PromotionThreatBonus { get; init; } = 45;
    public int NearPromotionBonus { get; init; } = 20;
    public int VulnerableManPenalty { get; init; } = 55;
    public int VulnerableDamaPenalty { get; init; } = 140;
    public int ProtectedManBonus { get; init; } = 10;
    public int TempoBonus { get; init; } = 2;
    public int BestCaptureSequenceBonus { get; init; } = 30;

    public static EvaluationWeights Default { get; } = new EvaluationWeights();

    public static EvaluationWeights Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A weights file path is required.", nameof(filePath));
        }

        string json = File.ReadAllText(filePath);
        EvaluationWeights? weights = JsonSerializer.Deserialize<EvaluationWeights>(json, JsonOptions());
        return weights ?? Default;
    }

    public void Save(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A weights file path is required.", nameof(filePath));
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions()));
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
