using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public class OuvOpeningBookImporterTests
{
    [Test]
    public void SquareNumberMapping_MatchesInitialBoardOrientation()
    {
        Assert.That(OuvOpeningBookImporter.SquareNumberToCoordinate(1), Is.EqualTo((0, 0)));
        Assert.That(OuvOpeningBookImporter.SquareNumberToCoordinate(4), Is.EqualTo((0, 6)));
        Assert.That(OuvOpeningBookImporter.SquareNumberToCoordinate(5), Is.EqualTo((1, 1)));
        Assert.That(OuvOpeningBookImporter.SquareNumberToCoordinate(32), Is.EqualTo((7, 7)));
        Assert.That(OuvOpeningBookImporter.CoordinateToSquareNumber((2, 0)), Is.EqualTo(9));
        Assert.That(OuvOpeningBookImporter.CoordinateToSquareNumber((5, 1)), Is.EqualTo(21));
    }

    [Test]
    public void ImportText_ParsesLegacyOpeningLinesIntoGameDatabase()
    {
        const string text = " 9-13 21-17  5- 9 25-21\r\n10-14 22-18  5-10";

        GameDatabase database = OuvOpeningBookImporter.ImportText(text, "sample.ouv");

        Assert.That(database.GameCount, Is.EqualTo(2));
        Assert.That(database.Games[0].InitialPlayer, Is.EqualTo(-1));
        Assert.That(database.Games[0].Moves.Count, Is.GreaterThanOrEqualTo(4));
        Assert.That(database.Games[0].Moves[0].Notation, Is.EqualTo("3,1-4,2"));
    }

    [Test]
    public void ImportedOuvDatabase_CanBuildLegalOpeningBook()
    {
        const string text = " 9-13 21-17  5- 9 25-21\r\n 9-13 21-18  5- 9 25-21";
        GameDatabase database = OuvOpeningBookImporter.ImportText(text, "sample.ouv");

        OpeningBook book = OpeningBook.Build(database, maxBookPlies: 4);
        int[,] board = GameController.CreateInitialBoard();
        MoveGenerator generator = new MoveGenerator();
        var legalMoves = generator.GetPlayerCapturesOrMoves(board, -1);
        OpeningBookRecommendation? recommendation = book.RecommendMove(board, -1, legalMoves, OpeningBookMode.MostPlayed);

        Assert.That(book.EntryCount, Is.GreaterThan(0));
        Assert.That(recommendation, Is.Not.Null);
        Assert.That(legalMoves.Any(move => OpeningBook.ComputeMoveKey(move) == OpeningBook.ComputeMoveKey(recommendation!.Move)), Is.True);
    }
}
