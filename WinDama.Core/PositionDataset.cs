using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinDama.Core;

/// <summary>
/// Collection of position samples prepared for evaluation tuning, classical ML,
/// or future NNUE-style training pipelines.
/// </summary>
public sealed class PositionDataset
{
    public string Name { get; init; } = "WinDama position dataset";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public List<PositionSample> Samples { get; init; } = new List<PositionSample>();

    public int SampleCount => Samples.Count;

    public void SaveJson(string filePath)
    {
        EnsureDirectory(filePath);
        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static PositionDataset LoadJson(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<PositionDataset>(json, JsonOptions) ?? new PositionDataset();
    }

    public void SaveJsonLines(string filePath)
    {
        EnsureDirectory(filePath);
        using StreamWriter writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        foreach (PositionSample sample in Samples)
        {
            writer.WriteLine(JsonSerializer.Serialize(sample, JsonOptionsCompact));
        }
    }

    public void SaveCsv(string filePath)
    {
        EnsureDirectory(filePath);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("sample_id,source,source_game_id,ply,player_to_move,player_profile,opponent_profile,evaluation,search_depth,nodes,quiescence_nodes,winner,result,best_move,best_move_key,pv,top_moves,player1_men,player1_damas,player2_men,player2_damas,position_key");
        foreach (PositionSample sample in Samples)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(sample.SampleId),
                Csv(sample.Source),
                Csv(sample.SourceGameId),
                sample.Ply.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.PlayerToMove.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Csv(sample.PlayerProfile),
                Csv(sample.OpponentProfile),
                sample.Evaluation.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.SearchDepth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.Nodes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.QuiescenceNodes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.Winner.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Csv(sample.ResultText),
                Csv(sample.BestMove),
                Csv(sample.BestMoveKey),
                Csv(string.Join(" | ", sample.PrincipalVariation)),
                Csv(string.Join(" | ", sample.TopMoves)),
                sample.PlayerOneMen.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.PlayerOneDamas.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.PlayerTwoMen.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sample.PlayerTwoDamas.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Csv(sample.PositionKey)
            }));
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    private static void EnsureDirectory(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value;
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions JsonOptionsCompact = new JsonSerializerOptions
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
