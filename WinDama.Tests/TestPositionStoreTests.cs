using System;
using System.IO;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class TestPositionStoreTests
{
    [Test]
    public void TestPosition_RoundTripsBoardAndSideToMove()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[2, 4] = -2;

        TestPosition position = TestPosition.FromState(
            "Dama tactic",
            "Regression test position",
            board,
            currentPlayer: -1,
            gameMode: GameMode.HumanVsHuman);

        string filePath = Path.Combine(Path.GetTempPath(), $"windama-position-{Guid.NewGuid():N}.json");
        try
        {
            TestPositionStore.Save(filePath, position);
            TestPosition loaded = TestPositionStore.Load(filePath);
            int[,] loadedBoard = loaded.ToBoard();

            Assert.That(loaded.Name, Is.EqualTo("Dama tactic"));
            Assert.That(loaded.CurrentPlayer, Is.EqualTo(-1));
            Assert.That(loaded.GameMode, Is.EqualTo(GameMode.HumanVsHuman));
            Assert.That(loadedBoard[5, 1], Is.EqualTo(1));
            Assert.That(loadedBoard[2, 4], Is.EqualTo(-2));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Test]
    public void TestPosition_RejectsInvalidPieceValue()
    {
        TestPosition position = TestPosition.FromState("Invalid", "", TestBoards.Empty(), 1, GameMode.HumanVsHuman);
        position.BoardRows[0][0] = 99;

        Assert.Throws<InvalidOperationException>(() => position.Validate());
    }
}
