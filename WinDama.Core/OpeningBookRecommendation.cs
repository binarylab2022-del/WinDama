namespace WinDama.Core;

public sealed class OpeningBookRecommendation
{
    public Move Move { get; init; } = new Move((0, 0), (0, 0));
    public OpeningBookEntry Entry { get; init; } = new OpeningBookEntry();
    public string Reason { get; init; } = string.Empty;
}
