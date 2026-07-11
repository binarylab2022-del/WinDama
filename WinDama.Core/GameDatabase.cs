using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinDama.Core;

/// <summary>
/// Simple JSON game database used to collect tournament/self-play games.
/// </summary>
public sealed class GameDatabase
{
    public string Name { get; init; } = "WinDama game database";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public List<GameRecord> Games { get; init; } = new List<GameRecord>();

    public int GameCount => Games.Count;
    public int TotalPlies => Games.Sum(game => game.PlyCount);

    public void Add(GameRecord game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        Games.Add(game);
    }

    public void AddTournament(EvaluationTournamentSummary summary)
    {
        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        foreach (EvaluationTournamentGameResult game in summary.Games)
        {
            Games.Add(GameRecord.FromTournamentGame(game));
        }
    }

    public void Save(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static GameDatabase Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<GameDatabase>(json, JsonOptions) ?? new GameDatabase();
    }

    public static GameDatabase FromTournament(EvaluationTournamentSummary summary)
    {
        GameDatabase database = new GameDatabase { Name = $"Tournament: {summary.ProfileA} vs {summary.ProfileB}" };
        database.AddTournament(summary);
        return database;
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
