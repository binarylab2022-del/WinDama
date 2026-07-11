namespace WinDama.Core;

/// <summary>
/// Defines who controls each side. The UI only presents labels for these modes;
/// the controller uses this enum to decide whether the side to move is human or AI.
/// </summary>
public enum GameMode
{
    HumanVsAI,
    HumanVsHuman,
    AIVsAI
}
