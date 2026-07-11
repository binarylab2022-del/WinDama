using System;
using System.Collections.Generic;

namespace WinDama.Core;

/// <summary>
/// Options for the simple benchmark-driven evaluation-weight tuner.
/// The first tuner is deliberately conservative: it tests one-weight-at-a-time
/// variants around a base profile, then ranks them with the tactical benchmark.
/// </summary>
public sealed class EvaluationWeightTuningOptions
{
    public TacticalBenchmarkOptions BenchmarkOptions { get; init; } = new TacticalBenchmarkOptions
    {
        Depth = 3,
        ForcedCaptureComparisonDepth = 3,
        MaximumPositions = int.MaxValue,
        RandomizeNearBestMoves = false
    };

    public int VariationPercent { get; init; } = 15;
    public int MinimumStep { get; init; } = 1;
    public int MaximumVariants { get; init; } = 32;
    public bool IncludeBaseProfile { get; init; } = true;
    public IReadOnlyList<string> WeightNames { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EffectiveWeightNames => WeightNames.Count > 0
        ? WeightNames
        : DefaultTunableWeights;

    public static readonly IReadOnlyList<string> DefaultTunableWeights = new[]
    {
        nameof(EvaluationWeights.DamaValue),
        nameof(EvaluationWeights.WinLikeCaptureSwing),
        nameof(EvaluationWeights.MobilityWeight),
        nameof(EvaluationWeights.DamaMobilityWeight),
        nameof(EvaluationWeights.CenterManBonus),
        nameof(EvaluationWeights.CenterDamaBonus),
        nameof(EvaluationWeights.EdgeSafetyBonus),
        nameof(EvaluationWeights.BackRowGuardBonus),
        nameof(EvaluationWeights.AdvancedManWeight),
        nameof(EvaluationWeights.PromotionThreatBonus),
        nameof(EvaluationWeights.NearPromotionBonus),
        nameof(EvaluationWeights.VulnerableManPenalty),
        nameof(EvaluationWeights.VulnerableDamaPenalty),
        nameof(EvaluationWeights.ProtectedManBonus),
        nameof(EvaluationWeights.BestCaptureSequenceBonus)
    };
}
