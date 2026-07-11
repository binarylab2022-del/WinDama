using System.Collections.Generic;

namespace WinDama.Core;

public class Move
{
    public (int, int) Start { get; set; } // Can be modified if needed
    public (int, int) End { get; set; } // Immutable
    public List<(int, int)> CapturedPieces { get; set; } // Immutable

    // Constructor for moves with captures
    public Move((int, int) start, (int, int) end, List<(int, int)> capturedPieces)
    {
        Start = start;
        End = end;
        CapturedPieces = capturedPieces;
    }

    // Constructor for moves without captures
    public Move((int, int) start, (int, int) end)
    {
        Start = start;
        End = end;
        CapturedPieces = new List<(int, int)>();
    }
}
