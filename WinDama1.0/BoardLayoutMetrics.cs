using System;
using System.Windows.Controls;

namespace WinDama;

internal sealed class BoardLayoutMetrics
{
    public double SquareSize { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double LabelMargin { get; set; }
    public double BoardSize => SquareSize * 8.0;
}

internal static class BoardLayoutCalculator
{
    public static BoardLayoutMetrics GetMetrics(Canvas canvas, int fallbackSquareSize, int fallbackOffset)
    {
        double width = canvas?.ActualWidth > 0 ? canvas.ActualWidth : fallbackSquareSize * 8 + fallbackOffset * 2;
        double height = canvas?.ActualHeight > 0 ? canvas.ActualHeight : fallbackSquareSize * 8 + fallbackOffset * 2;
        double availableSide = Math.Max(160.0, Math.Min(width, height));
        double padding = Math.Max(6.0, Math.Min(fallbackOffset, availableSide * 0.035));
        double labelMargin = Math.Max(18.0, Math.Min(38.0, availableSide * 0.06));
        double squareSize = Math.Max(16.0, (availableSide - 2.0 * padding - 2.0 * labelMargin) / 8.0);
        double boardSize = squareSize * 8.0;

        return new BoardLayoutMetrics
        {
            SquareSize = squareSize,
            LabelMargin = labelMargin,
            OffsetX = Math.Max(labelMargin, (width - boardSize) / 2.0),
            OffsetY = Math.Max(labelMargin, (height - boardSize) / 2.0)
        };
    }
}
