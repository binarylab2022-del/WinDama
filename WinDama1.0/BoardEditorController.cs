using System.Text.RegularExpressions;
using System.Windows.Controls;
using WinDama.Core;

namespace WinDama
{
    /// <summary>
    /// Small UI helper for board-editor values and test-position metadata.
    /// It has no responsibility for rules or move execution.
    /// </summary>
    public sealed class BoardEditorController
    {
        private readonly ComboBox pieceComboBox;
        private readonly ComboBox sideToMoveComboBox;
        private readonly TextBlock hintTextBlock;

        public BoardEditorController(ComboBox pieceComboBox, ComboBox sideToMoveComboBox, TextBlock hintTextBlock)
        {
            this.pieceComboBox = pieceComboBox;
            this.sideToMoveComboBox = sideToMoveComboBox;
            this.hintTextBlock = hintTextBlock;
        }

        public int GetSelectedPieceValue(int fallback)
        {
            if (pieceComboBox?.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int value))
            {
                return value;
            }

            return fallback;
        }

        public int GetSideToMove()
        {
            return sideToMoveComboBox?.SelectedIndex == 1 ? -1 : 1;
        }

        public void SetSideToMove(int player)
        {
            if (sideToMoveComboBox != null)
            {
                sideToMoveComboBox.SelectedIndex = player < 0 ? 1 : 0;
            }
        }

        public void SetSelectedPieceValue(int value)
        {
            if (pieceComboBox == null)
            {
                return;
            }

            for (int i = 0; i < pieceComboBox.Items.Count; i++)
            {
                if (pieceComboBox.Items[i] is ComboBoxItem item &&
                    int.TryParse(item.Tag?.ToString(), out int itemValue) &&
                    itemValue == value)
                {
                    pieceComboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        public void UpdateHint(bool editorEnabled, int selectedPieceValue, int currentPlayer)
        {
            if (hintTextBlock == null)
            {
                return;
            }

            string modeText = editorEnabled ? "ON" : "OFF";
            hintTextBlock.Text = $"Editor {modeText}. Click places: {DescribePieceValue(selectedPieceValue)}. Side to move: {GameController.GetPlayerLabel(currentPlayer)}.";
        }

        public static bool IsInsideBoard(int row, int column)
        {
            return row >= 0 && row < 8 && column >= 0 && column < 8;
        }

        public static bool IsPlayableSquare(int row, int column)
        {
            return (row + column) % 2 == 0;
        }

        public static string DescribePieceValue(int value)
        {
            return value switch
            {
                1 => "Player 1 man",
                2 => "Player 1 Dama",
                -1 => "Player 2 man",
                -2 => "Player 2 Dama",
                _ => "Empty square"
            };
        }

        public static int CountPieces(int[,] sourceBoard)
        {
            int count = 0;
            for (int row = 0; row < sourceBoard.GetLength(0); row++)
            {
                for (int column = 0; column < sourceBoard.GetLength(1); column++)
                {
                    if (sourceBoard[row, column] != 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public static string BuildSafePositionFileName(string text)
        {
            string source = string.IsNullOrWhiteSpace(text) ? "windama-position" : text.Trim();
            string safe = Regex.Replace(source, "[^A-Za-z0-9_-]+", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = "windama-position";
            }

            return safe + ".json";
        }
    }
}
