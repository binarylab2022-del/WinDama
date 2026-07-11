namespace WinDama.Tests;

internal static class TestBoards
{
    public static int[,] Empty()
    {
        return new int[8, 8];
    }

    public static int[,] InitialPosition()
    {
        return new int[8, 8]
        {
            { -1, 0, -1, 0, -1, 0, -1, 0 },
            { 0, -1, 0, -1, 0, -1, 0, -1 },
            { -1, 0, -1, 0, -1, 0, -1, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 1, 0, 1, 0, 1, 0, 1 },
            { 1, 0, 1, 0, 1, 0, 1, 0 },
            { 0, 1, 0, 1, 0, 1, 0, 1 }
        };
    }
}
