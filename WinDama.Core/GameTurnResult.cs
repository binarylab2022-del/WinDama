namespace WinDama.Core;

public sealed class GameTurnResult
{
    public bool MoveApplied { get; init; }
    public bool TurnSwitched { get; init; }
    public int PlayerBeforeMove { get; init; }
    public int CurrentPlayer { get; init; }
    public Move? Move { get; init; }
    public GameStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;

    public bool IsGameOver => Status != GameStatus.Ongoing;
}
