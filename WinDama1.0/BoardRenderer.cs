using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WinDama.Core;

namespace WinDama
{
    /// <summary>
    /// Draws the board, coordinates, pieces, legal-move markers, and last-move markers.
    /// It intentionally contains no game-rule logic.
    /// </summary>
    public sealed class BoardRenderer
    {
        private readonly int defaultSquareSize;
        private readonly int defaultBoardOffset;

        public BoardRenderer(int defaultSquareSize, int defaultBoardOffset)
        {
            this.defaultSquareSize = defaultSquareSize;
            this.defaultBoardOffset = defaultBoardOffset;
        }

        public bool TryGetBoardCellFromPoint(Canvas canvas, Point point, out int row, out int column)
        {
            BoardLayoutMetrics layout = BoardLayoutCalculator.GetMetrics(canvas, defaultSquareSize, defaultBoardOffset);
            row = -1;
            column = -1;

            if (point.X < layout.OffsetX || point.Y < layout.OffsetY ||
                point.X >= layout.OffsetX + layout.BoardSize ||
                point.Y >= layout.OffsetY + layout.BoardSize)
            {
                return false;
            }

            column = (int)((point.X - layout.OffsetX) / layout.SquareSize);
            row = (int)((point.Y - layout.OffsetY) / layout.SquareSize);

            return row >= 0 && row < 8 && column >= 0 && column < 8;
        }

        public void Draw(
            Canvas canvas,
            int[,] board,
            bool pieceSelected,
            int? selectedRow,
            int? selectedColumn,
            IReadOnlyList<Move> highlightedLegalMoves,
            Move lastMove)
        {
            if (canvas == null || board == null)
            {
                return;
            }

            canvas.Children.Clear();
            BoardLayoutMetrics layout = BoardLayoutCalculator.GetMetrics(canvas, defaultSquareSize, defaultBoardOffset);
            double squareSize = layout.SquareSize;
            double pieceMargin = Math.Max(4.0, squareSize * 0.08);

            DrawBoardNotation(canvas, layout);

            for (int row = 0; row < board.GetLength(0); row++)
            {
                for (int column = 0; column < board.GetLength(1); column++)
                {
                    bool isSelectedSquare = pieceSelected && selectedRow == row && selectedColumn == column;
                    Move highlightedMove = highlightedLegalMoves?.FirstOrDefault(m => m.End.Item1 == row && m.End.Item2 == column);
                    bool isMoveTarget = highlightedMove != null;
                    bool isCaptureTarget = isMoveTarget && highlightedMove.CapturedPieces.Count > 0;
                    bool isLastMoveStart = lastMove != null && lastMove.Start.Item1 == row && lastMove.Start.Item2 == column;
                    bool isLastMoveEnd = lastMove != null && lastMove.End.Item1 == row && lastMove.End.Item2 == column;
                    bool isLastCapturedSquare = lastMove != null && lastMove.CapturedPieces.Any(c => c.Item1 == row && c.Item2 == column);
                    bool isLastMoveSquare = isLastMoveStart || isLastMoveEnd;

                    Brush strokeBrush = Brushes.Transparent;
                    double strokeThickness = 1;
                    if (isLastMoveSquare)
                    {
                        strokeBrush = Brushes.Gold;
                        strokeThickness = 4;
                    }

                    if (isSelectedSquare)
                    {
                        strokeBrush = Brushes.DodgerBlue;
                        strokeThickness = 4;
                    }

                    Rectangle square = new Rectangle
                    {
                        Width = squareSize,
                        Height = squareSize,
                        Fill = GetSquareBrush(row, column, isSelectedSquare, isMoveTarget, isCaptureTarget, isLastMoveSquare, isLastCapturedSquare),
                        Stroke = strokeBrush,
                        StrokeThickness = strokeThickness,
                        Tag = Tuple.Create(row, column)
                    };

                    Canvas.SetLeft(square, layout.OffsetX + column * squareSize);
                    Canvas.SetTop(square, layout.OffsetY + row * squareSize);
                    canvas.Children.Add(square);

                    if (isLastMoveSquare && !isSelectedSquare && !isMoveTarget)
                    {
                        TextBlock marker = new TextBlock
                        {
                            Text = isLastMoveStart ? "•" : "✓",
                            FontSize = Math.Max(14.0, squareSize * 0.33),
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.DarkGoldenrod,
                            IsHitTestVisible = false
                        };

                        Canvas.SetLeft(marker, layout.OffsetX + column * squareSize + squareSize * 0.10);
                        Canvas.SetTop(marker, layout.OffsetY + row * squareSize + squareSize * 0.04);
                        canvas.Children.Add(marker);
                    }

                    if (isMoveTarget)
                    {
                        Ellipse marker = new Ellipse
                        {
                            Width = isCaptureTarget ? squareSize * 0.35 : squareSize * 0.23,
                            Height = isCaptureTarget ? squareSize * 0.35 : squareSize * 0.23,
                            Fill = isCaptureTarget ? Brushes.OrangeRed : Brushes.LightGreen,
                            Stroke = Brushes.Black,
                            StrokeThickness = isCaptureTarget ? 2 : 1,
                            Opacity = 0.85,
                            IsHitTestVisible = false
                        };

                        Canvas.SetLeft(marker, layout.OffsetX + column * squareSize + (squareSize - marker.Width) / 2);
                        Canvas.SetTop(marker, layout.OffsetY + row * squareSize + (squareSize - marker.Height) / 2);
                        canvas.Children.Add(marker);
                    }

                    if (isLastCapturedSquare)
                    {
                        Ellipse captureMarker = new Ellipse
                        {
                            Width = squareSize * 0.20,
                            Height = squareSize * 0.20,
                            Fill = Brushes.IndianRed,
                            Stroke = Brushes.White,
                            StrokeThickness = 2,
                            Opacity = 0.90,
                            IsHitTestVisible = false
                        };

                        Canvas.SetLeft(captureMarker, layout.OffsetX + column * squareSize + (squareSize - captureMarker.Width) / 2);
                        Canvas.SetTop(captureMarker, layout.OffsetY + row * squareSize + (squareSize - captureMarker.Height) / 2);
                        canvas.Children.Add(captureMarker);
                    }

                    if (board[row, column] != 0)
                    {
                        Image pieceImage = new Image
                        {
                            Width = Math.Max(8.0, squareSize - 2.0 * pieceMargin),
                            Height = Math.Max(8.0, squareSize - 2.0 * pieceMargin),
                            Source = GetPieceImage(board[row, column]),
                            IsHitTestVisible = false
                        };

                        Canvas.SetLeft(pieceImage, layout.OffsetX + column * squareSize + pieceMargin);
                        Canvas.SetTop(pieceImage, layout.OffsetY + row * squareSize + pieceMargin);
                        canvas.Children.Add(pieceImage);
                    }
                }
            }
        }

        private static void DrawBoardNotation(Canvas canvas, BoardLayoutMetrics layout)
        {
            double squareSize = layout.SquareSize;
            double labelMargin = layout.LabelMargin;
            double fontSize = Math.Max(10.0, Math.Min(18.0, squareSize * 0.24));

            for (int index = 0; index < 8; index++)
            {
                string label = (index + 1).ToString();

                AddNotationLabel(canvas, label,
                    layout.OffsetX + index * squareSize,
                    layout.OffsetY - labelMargin,
                    squareSize, labelMargin, fontSize);

                AddNotationLabel(canvas, label,
                    layout.OffsetX + index * squareSize,
                    layout.OffsetY + layout.BoardSize,
                    squareSize, labelMargin, fontSize);

                AddNotationLabel(canvas, label,
                    layout.OffsetX - labelMargin,
                    layout.OffsetY + index * squareSize,
                    labelMargin, squareSize, fontSize);

                AddNotationLabel(canvas, label,
                    layout.OffsetX + layout.BoardSize,
                    layout.OffsetY + index * squareSize,
                    labelMargin, squareSize, fontSize);
            }
        }

        private static void AddNotationLabel(Canvas canvas, string text, double left, double top, double width, double height, double fontSize)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                Width = width,
                Height = height,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DimGray,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(label, left);
            Canvas.SetTop(label, top + Math.Max(0.0, (height - fontSize * 1.35) / 2.0));
            canvas.Children.Add(label);
        }

        private static Brush GetSquareBrush(int row, int column, bool isSelectedSquare, bool isMoveTarget, bool isCaptureTarget, bool isLastMoveSquare, bool isLastCapturedSquare)
        {
            if (isSelectedSquare)
            {
                return Brushes.LightSkyBlue;
            }

            if (isCaptureTarget)
            {
                return Brushes.Orange;
            }

            if (isMoveTarget)
            {
                return Brushes.LightGreen;
            }

            if (isLastCapturedSquare)
            {
                return Brushes.MistyRose;
            }

            if (isLastMoveSquare)
            {
                return Brushes.Khaki;
            }

            return (row + column) % 2 == 0 ? Brushes.White : Brushes.Green;
        }

        private static ImageSource? GetPieceImage(int pieceValue)
        {
            string imagePath = pieceValue switch
            {
                1 => "pack://application:,,,/Resources/1.png",
                2 => "pack://application:,,,/Resources/3.png",
                -1 => "pack://application:,,,/Resources/2.png",
                -2 => "pack://application:,,,/Resources/4.png",
                _ => string.Empty
            };

            return string.IsNullOrWhiteSpace(imagePath) ? null : new BitmapImage(new Uri(imagePath));
        }
    }
}
