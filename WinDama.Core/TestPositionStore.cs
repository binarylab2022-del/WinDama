using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinDama.Core;

public static class TestPositionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(string filePath, TestPosition position)
    {
        position.Validate();
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(position, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static TestPosition Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        TestPosition? position = JsonSerializer.Deserialize<TestPosition>(json, JsonOptions);
        if (position == null)
        {
            throw new InvalidDataException("The file does not contain a valid WinDama test position.");
        }

        position.Validate();
        return position;
    }
}
