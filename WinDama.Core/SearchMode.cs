namespace WinDama.Core;

/// <summary>
/// Describes how the AI search budget is interpreted.
/// The WPF layer may expose these modes with localized labels, but the core
/// engine only needs this neutral enum.
/// </summary>
public enum SearchMode
{
    FixedDepth,
    FixedTimePerMove,
    GameClock
}
