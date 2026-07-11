using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class MoveOrderingTests
{
    [Test]
    public void OrderMoves_PutsTranspositionBestMoveFirst()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[5, 5] = 1;

        Move ordinaryMove = new Move((5, 1), (4, 0));
        Move ttBestMove = new Move((5, 5), (4, 4));
        List<Move> moves = new List<Move> { ordinaryMove, ttBestMove };

        List<Move> ordered = InvokeOrderMoves(board, moves, ttBestMove);

        Assert.That(SameMove(ordered.First(), ttBestMove), Is.True);
    }

    [Test]
    public void OrderMoves_PrefersHigherValueCapture()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;
        board[4, 2] = -2;
        board[5, 5] = 1;
        board[4, 6] = -1;

        Move capturesDama = new Move((5, 1), (3, 3), new List<(int, int)> { (4, 2) });
        Move capturesMan = new Move((5, 5), (3, 7), new List<(int, int)> { (4, 6) });
        List<Move> moves = new List<Move> { capturesMan, capturesDama };

        List<Move> ordered = InvokeOrderMoves(board, moves, transpositionBestMove: null);

        Assert.That(SameMove(ordered.First(), capturesDama), Is.True);
    }

    [Test]
    public void OrderMoves_PrefersQuietPromotionOverOrdinaryQuietMove()
    {
        int[,] board = TestBoards.Empty();
        board[1, 1] = 1;
        board[5, 5] = 1;

        Move promotionMove = new Move((1, 1), (0, 0));
        Move ordinaryMove = new Move((5, 5), (4, 4));
        List<Move> moves = new List<Move> { ordinaryMove, promotionMove };

        List<Move> ordered = InvokeOrderMoves(board, moves, transpositionBestMove: null);

        Assert.That(SameMove(ordered.First(), promotionMove), Is.True);
    }

    private static List<Move> InvokeOrderMoves(int[,] board, List<Move> moves, Move? transpositionBestMove)
    {
        var engine = new SearchEngine(new MoveGenerator(), new Evaluation(), new System.Random(1));
        MethodInfo method = typeof(SearchEngine).GetMethod("OrderMoves", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<Move>)method.Invoke(engine, new object?[] { board, moves, transpositionBestMove })!;
    }

    private static bool SameMove(Move left, Move right)
    {
        return left.Start == right.Start
            && left.End == right.End
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }
}
