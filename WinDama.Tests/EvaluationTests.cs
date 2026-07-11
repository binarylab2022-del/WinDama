using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public class EvaluationTests
{
    private readonly Evaluation evaluation = new Evaluation();

    [Test]
    public void EvaluateBoard_ExtraMan_IsPositiveForOwningSide()
    {
        int[,] board = TestBoards.Empty();
        board[5, 1] = 1;

        Assert.That(evaluation.EvaluateBoard(board, 1), Is.GreaterThan(0));
        Assert.That(evaluation.EvaluateBoard(board, -1), Is.LessThan(0));
    }

    [Test]
    public void EvaluateBoard_Dama_IsWorthMoreThanMan()
    {
        int[,] manBoard = TestBoards.Empty();
        manBoard[5, 1] = 1;

        int[,] damaBoard = TestBoards.Empty();
        damaBoard[5, 1] = 2;

        Assert.That(evaluation.EvaluateBoard(damaBoard, 1), Is.GreaterThan(evaluation.EvaluateBoard(manBoard, 1)));
    }

    [Test]
    public void EvaluateBreakdown_PromotionThreat_IsReported()
    {
        int[,] board = TestBoards.Empty();
        board[1, 2] = 1;

        EvaluationBreakdown breakdown = evaluation.EvaluateBreakdown(board, 1);

        Assert.That(breakdown.PromotionThreats, Is.GreaterThan(0));
        Assert.That(breakdown.Total, Is.EqualTo(evaluation.EvaluateBoard(board, 1)));
    }

    [Test]
    public void EvaluateBreakdown_Vulnerability_PenalizesCapturablePiece()
    {
        int[,] safeBoard = TestBoards.Empty();
        safeBoard[5, 1] = 1;

        int[,] vulnerableBoard = TestBoards.Empty();
        vulnerableBoard[5, 1] = 1;
        vulnerableBoard[4, 2] = -1;

        EvaluationBreakdown safe = evaluation.EvaluateBreakdown(safeBoard, 1);
        EvaluationBreakdown vulnerable = evaluation.EvaluateBreakdown(vulnerableBoard, 1);

        Assert.That(vulnerable.Vulnerability, Is.LessThan(safe.Vulnerability));
    }
}
