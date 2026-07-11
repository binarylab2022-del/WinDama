using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Simple benchmark-driven tuner for the handcrafted evaluator.
/// It generates local one-weight variants around a base profile and ranks them
/// using the tactical benchmark scoring system.
/// </summary>
public sealed class EvaluationWeightTuner
{
    private readonly TacticalBenchmarkRunner benchmarkRunner;

    public EvaluationWeightTuner()
        : this(new TacticalBenchmarkRunner())
    {
    }

    public EvaluationWeightTuner(TacticalBenchmarkRunner benchmarkRunner)
    {
        this.benchmarkRunner = benchmarkRunner ?? throw new ArgumentNullException(nameof(benchmarkRunner));
    }

    public EvaluationWeightTuningSummary Tune(
        string positionsDirectory,
        EvaluationProfile baseProfile,
        EvaluationWeightTuningOptions? options = null)
    {
        if (baseProfile == null)
        {
            throw new ArgumentNullException(nameof(baseProfile));
        }

        options ??= new EvaluationWeightTuningOptions();
        List<EvaluationWeightTuningResult> results = new List<EvaluationWeightTuningResult>();

        foreach (EvaluationProfile candidate in GenerateCandidates(baseProfile, options))
        {
            TacticalBenchmarkSummary benchmark = benchmarkRunner.Run(
                positionsDirectory,
                new[] { candidate },
                options.BenchmarkOptions);

            results.Add(new EvaluationWeightTuningResult
            {
                CandidateName = candidate.Name,
                ChangedWeightName = CandidateChangedWeight(candidate.Name),
                OriginalValue = CandidateOriginalValue(candidate.Name),
                TunedValue = CandidateTunedValue(candidate.Name),
                Weights = candidate.Weights,
                BenchmarkSummary = benchmark
            });
        }

        return new EvaluationWeightTuningSummary
        {
            BaseProfileName = baseProfile.Name,
            BaseWeights = baseProfile.Weights,
            Results = results
        };
    }

    public IReadOnlyList<EvaluationProfile> GenerateCandidates(EvaluationProfile baseProfile, EvaluationWeightTuningOptions? options = null)
    {
        if (baseProfile == null)
        {
            throw new ArgumentNullException(nameof(baseProfile));
        }

        options ??= new EvaluationWeightTuningOptions();
        int variantLimit = Math.Max(1, options.MaximumVariants);
        int percent = Math.Max(1, options.VariationPercent);
        int minimumStep = Math.Max(1, options.MinimumStep);

        List<EvaluationProfile> candidates = new List<EvaluationProfile>();
        if (options.IncludeBaseProfile)
        {
            candidates.Add(new EvaluationProfile($"{baseProfile.Name} | base", baseProfile.Weights));
        }

        foreach (string weightName in options.EffectiveWeightNames)
        {
            if (candidates.Count >= variantLimit)
            {
                break;
            }

            int currentValue = GetWeightValue(baseProfile.Weights, weightName);
            int step = Math.Max(minimumStep, (int)Math.Round(Math.Abs(currentValue) * percent / 100.0));
            if (step == 0)
            {
                step = minimumStep;
            }

            foreach (int direction in new[] { -1, 1 })
            {
                if (candidates.Count >= variantLimit)
                {
                    break;
                }

                int tunedValue = Math.Max(0, currentValue + direction * step);
                if (tunedValue == currentValue)
                {
                    continue;
                }

                string sign = direction > 0 ? "+" : "-";
                string name = $"{baseProfile.Name} | {weightName}{sign}{percent}% [{currentValue}->{tunedValue}]";
                candidates.Add(new EvaluationProfile(name, WithWeight(baseProfile.Weights, weightName, tunedValue)));
            }
        }

        return candidates;
    }

    private static string CandidateChangedWeight(string candidateName)
    {
        int pipe = candidateName.IndexOf('|');
        if (pipe < 0)
        {
            return string.Empty;
        }

        string tail = candidateName[(pipe + 1)..].Trim();
        if (tail.Equals("base", StringComparison.OrdinalIgnoreCase))
        {
            return "base";
        }

        int sign = tail.IndexOf('+');
        if (sign < 0)
        {
            sign = tail.IndexOf('-');
        }

        return sign > 0 ? tail[..sign] : tail;
    }

    private static int? CandidateOriginalValue(string candidateName)
    {
        var pair = CandidateValuePair(candidateName);
        return pair.original;
    }

    private static int? CandidateTunedValue(string candidateName)
    {
        var pair = CandidateValuePair(candidateName);
        return pair.tuned;
    }

    private static (int? original, int? tuned) CandidateValuePair(string candidateName)
    {
        int open = candidateName.LastIndexOf('[');
        int arrow = candidateName.LastIndexOf("->", StringComparison.Ordinal);
        int close = candidateName.LastIndexOf(']');
        if (open < 0 || arrow < 0 || close < 0 || arrow <= open || close <= arrow)
        {
            return (null, null);
        }

        bool hasOriginal = int.TryParse(candidateName.Substring(open + 1, arrow - open - 1), out int original);
        bool hasTuned = int.TryParse(candidateName.Substring(arrow + 2, close - arrow - 2), out int tuned);
        return (hasOriginal ? original : null, hasTuned ? tuned : null);
    }

    private static int GetWeightValue(EvaluationWeights weights, string weightName)
    {
        return weightName switch
        {
            nameof(EvaluationWeights.ManValue) => weights.ManValue,
            nameof(EvaluationWeights.DamaValue) => weights.DamaValue,
            nameof(EvaluationWeights.WinLikeCaptureSwing) => weights.WinLikeCaptureSwing,
            nameof(EvaluationWeights.MobilityWeight) => weights.MobilityWeight,
            nameof(EvaluationWeights.DamaMobilityWeight) => weights.DamaMobilityWeight,
            nameof(EvaluationWeights.CenterManBonus) => weights.CenterManBonus,
            nameof(EvaluationWeights.CenterDamaBonus) => weights.CenterDamaBonus,
            nameof(EvaluationWeights.EdgeSafetyBonus) => weights.EdgeSafetyBonus,
            nameof(EvaluationWeights.BackRowGuardBonus) => weights.BackRowGuardBonus,
            nameof(EvaluationWeights.AdvancedManWeight) => weights.AdvancedManWeight,
            nameof(EvaluationWeights.PromotionThreatBonus) => weights.PromotionThreatBonus,
            nameof(EvaluationWeights.NearPromotionBonus) => weights.NearPromotionBonus,
            nameof(EvaluationWeights.VulnerableManPenalty) => weights.VulnerableManPenalty,
            nameof(EvaluationWeights.VulnerableDamaPenalty) => weights.VulnerableDamaPenalty,
            nameof(EvaluationWeights.ProtectedManBonus) => weights.ProtectedManBonus,
            nameof(EvaluationWeights.TempoBonus) => weights.TempoBonus,
            nameof(EvaluationWeights.BestCaptureSequenceBonus) => weights.BestCaptureSequenceBonus,
            _ => throw new ArgumentException($"Unknown evaluation weight: {weightName}", nameof(weightName))
        };
    }

    private static EvaluationWeights WithWeight(EvaluationWeights weights, string weightName, int value)
    {
        return weightName switch
        {
            nameof(EvaluationWeights.ManValue) => Copy(weights, manValue: value),
            nameof(EvaluationWeights.DamaValue) => Copy(weights, damaValue: value),
            nameof(EvaluationWeights.WinLikeCaptureSwing) => Copy(weights, winLikeCaptureSwing: value),
            nameof(EvaluationWeights.MobilityWeight) => Copy(weights, mobilityWeight: value),
            nameof(EvaluationWeights.DamaMobilityWeight) => Copy(weights, damaMobilityWeight: value),
            nameof(EvaluationWeights.CenterManBonus) => Copy(weights, centerManBonus: value),
            nameof(EvaluationWeights.CenterDamaBonus) => Copy(weights, centerDamaBonus: value),
            nameof(EvaluationWeights.EdgeSafetyBonus) => Copy(weights, edgeSafetyBonus: value),
            nameof(EvaluationWeights.BackRowGuardBonus) => Copy(weights, backRowGuardBonus: value),
            nameof(EvaluationWeights.AdvancedManWeight) => Copy(weights, advancedManWeight: value),
            nameof(EvaluationWeights.PromotionThreatBonus) => Copy(weights, promotionThreatBonus: value),
            nameof(EvaluationWeights.NearPromotionBonus) => Copy(weights, nearPromotionBonus: value),
            nameof(EvaluationWeights.VulnerableManPenalty) => Copy(weights, vulnerableManPenalty: value),
            nameof(EvaluationWeights.VulnerableDamaPenalty) => Copy(weights, vulnerableDamaPenalty: value),
            nameof(EvaluationWeights.ProtectedManBonus) => Copy(weights, protectedManBonus: value),
            nameof(EvaluationWeights.TempoBonus) => Copy(weights, tempoBonus: value),
            nameof(EvaluationWeights.BestCaptureSequenceBonus) => Copy(weights, bestCaptureSequenceBonus: value),
            _ => throw new ArgumentException($"Unknown evaluation weight: {weightName}", nameof(weightName))
        };
    }

    private static EvaluationWeights Copy(
        EvaluationWeights source,
        int? manValue = null,
        int? damaValue = null,
        int? winLikeCaptureSwing = null,
        int? mobilityWeight = null,
        int? damaMobilityWeight = null,
        int? centerManBonus = null,
        int? centerDamaBonus = null,
        int? edgeSafetyBonus = null,
        int? backRowGuardBonus = null,
        int? advancedManWeight = null,
        int? promotionThreatBonus = null,
        int? nearPromotionBonus = null,
        int? vulnerableManPenalty = null,
        int? vulnerableDamaPenalty = null,
        int? protectedManBonus = null,
        int? tempoBonus = null,
        int? bestCaptureSequenceBonus = null)
    {
        return new EvaluationWeights
        {
            ManValue = manValue ?? source.ManValue,
            DamaValue = damaValue ?? source.DamaValue,
            WinLikeCaptureSwing = winLikeCaptureSwing ?? source.WinLikeCaptureSwing,
            MobilityWeight = mobilityWeight ?? source.MobilityWeight,
            DamaMobilityWeight = damaMobilityWeight ?? source.DamaMobilityWeight,
            CenterManBonus = centerManBonus ?? source.CenterManBonus,
            CenterDamaBonus = centerDamaBonus ?? source.CenterDamaBonus,
            EdgeSafetyBonus = edgeSafetyBonus ?? source.EdgeSafetyBonus,
            BackRowGuardBonus = backRowGuardBonus ?? source.BackRowGuardBonus,
            AdvancedManWeight = advancedManWeight ?? source.AdvancedManWeight,
            PromotionThreatBonus = promotionThreatBonus ?? source.PromotionThreatBonus,
            NearPromotionBonus = nearPromotionBonus ?? source.NearPromotionBonus,
            VulnerableManPenalty = vulnerableManPenalty ?? source.VulnerableManPenalty,
            VulnerableDamaPenalty = vulnerableDamaPenalty ?? source.VulnerableDamaPenalty,
            ProtectedManBonus = protectedManBonus ?? source.ProtectedManBonus,
            TempoBonus = tempoBonus ?? source.TempoBonus,
            BestCaptureSequenceBonus = bestCaptureSequenceBonus ?? source.BestCaptureSequenceBonus
        };
    }
}
