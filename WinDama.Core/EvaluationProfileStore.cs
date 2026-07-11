using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Loads one or more JSON evaluation-weight profiles from disk.
/// </summary>
public static class EvaluationProfileStore
{
    public static IReadOnlyList<EvaluationProfile> LoadProfiles(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A profile directory is required.", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Evaluation profile directory was not found: {directoryPath}");
        }

        List<EvaluationProfile> profiles = Directory
            .GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new EvaluationProfile(ProfileNameFromFile(path), EvaluationWeights.Load(path), path))
            .ToList();

        if (profiles.Count == 0)
        {
            profiles.Add(new EvaluationProfile("default", EvaluationWeights.Default));
        }

        return profiles;
    }

    private static string ProfileNameFromFile(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path)
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(name) ? "default" : name;
    }
}
