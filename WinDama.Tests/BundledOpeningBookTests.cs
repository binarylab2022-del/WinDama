using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

public sealed class BundledOpeningBookTests
{
    [Test]
    public void BundledOpeningBooks_AreCopiedToTestOutput()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "OpeningBooks");
        Assert.That(Directory.Exists(folder), Is.True, $"Missing bundled opening book folder: {folder}");

        string[] files = Directory.GetFiles(folder, "*.ouv");
        Assert.That(files.Length, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void BundledOpeningBooks_CanBuildLegalOpeningBook()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "OpeningBooks");
        string[] files = Directory.GetFiles(folder, "*.ouv");

        GameDatabase database = OuvOpeningBookImporter.ImportFiles(files, includeMirroredLines: true);
        Assert.That(database.GameCount, Is.GreaterThan(0));

        OpeningBook book = OpeningBook.Build(database, maxBookPlies: 16);
        Assert.That(book.EntryCount, Is.GreaterThan(0));

        int[,] board = GameController.CreateInitialBoard();
        MoveGenerator generator = new MoveGenerator();
        IReadOnlyList<Move> legalMoves = generator.GetPlayerCapturesOrMoves(board, 1);
        OpeningBookRecommendation? recommendation = book.RecommendMove(board, 1, legalMoves, OpeningBookMode.BestScore, minimumGames: 1);

        Assert.That(recommendation, Is.Not.Null);
        Assert.That(legalMoves.Any(move => OpeningBook.ComputeMoveKey(move) == OpeningBook.ComputeMoveKey(recommendation!.Move)), Is.True);
    }
}
