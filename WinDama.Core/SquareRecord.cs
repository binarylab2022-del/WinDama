namespace WinDama.Core;

/// <summary>
/// Serializable board coordinate used in game records and opening-book files.
/// Coordinates are zero-based internally, matching the engine board array.
/// </summary>
public sealed class SquareRecord
{
    public int Row { get; init; }
    public int Column { get; init; }

    public SquareRecord()
    {
    }

    public SquareRecord(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public (int Row, int Column) ToTuple() => (Row, Column);

    public static SquareRecord FromTuple((int Row, int Column) square)
    {
        return new SquareRecord(square.Row, square.Column);
    }
}
