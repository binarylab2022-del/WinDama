using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Sparse but deterministic numeric feature vector used by the lightweight
/// learned evaluator. Feature names are stable so models can be serialized and
/// compared across engine versions.
/// </summary>
public sealed class FeatureVector
{
    public Dictionary<string, double> Values { get; init; } = new Dictionary<string, double>(StringComparer.Ordinal);

    public double this[string name] => Values.TryGetValue(name, out double value) ? value : 0.0;

    public double Dot(IReadOnlyDictionary<string, double> weights)
    {
        if (weights == null)
        {
            throw new ArgumentNullException(nameof(weights));
        }

        double total = 0.0;
        foreach (KeyValuePair<string, double> feature in Values)
        {
            if (weights.TryGetValue(feature.Key, out double weight))
            {
                total += feature.Value * weight;
            }
        }

        return total;
    }

    public override string ToString()
    {
        return string.Join(", ", Values.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value:0.###}"));
    }
}
