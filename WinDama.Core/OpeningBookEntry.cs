namespace WinDama.Core;

/// <summary>
/// Aggregated statistics for one move from one position.
/// </summary>
public sealed class OpeningBookEntry
{
    public string PositionKey { get; init; } = string.Empty;
    public int Ply { get; init; }
    public string MoveKey { get; init; } = string.Empty;
    public string MoveNotation { get; init; } = string.Empty;
    public int Player { get; init; }
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public double Points { get; set; }
    public double ScorePercent => Games <= 0 ? 0 : 100.0 * Points / Games;
}
