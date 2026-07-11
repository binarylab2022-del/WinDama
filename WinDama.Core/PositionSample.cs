using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// One supervised-learning sample extracted from a searched position or a recorded game.
/// It is intentionally serializable and independent from WPF.
/// </summary>
public sealed class PositionSample
{
    public string SampleId { get; init; } = Guid.NewGuid().ToString("N");
    public string Source { get; init; } = string.Empty;
    public string SourceGameId { get; init; } = string.Empty;
    public int Ply { get; init; }
    public int PlayerToMove { get; init; }
    public string PlayerProfile { get; init; } = string.Empty;
    public string OpponentProfile { get; init; } = string.Empty;
    public int[][] Board { get; init; } = GameRecord.BoardToRows(GameController.CreateInitialBoard());
    public string PositionKey { get; init; } = string.Empty;
    public string BestMove { get; init; } = string.Empty;
    public string BestMoveKey { get; init; } = string.Empty;
    public List<string> PrincipalVariation { get; init; } = new List<string>();
    public List<string> TopMoves { get; init; } = new List<string>();
    public int Evaluation { get; init; }
    public int SearchDepth { get; init; }
    public long Nodes { get; init; }
    public long QuiescenceNodes { get; init; }
    public int Winner { get; init; }
    public string ResultText { get; init; } = string.Empty;
    public int GamePlyCount { get; init; }
    public int PlayerOneMen { get; init; }
    public int PlayerOneDamas { get; init; }
    public int PlayerTwoMen { get; init; }
    public int PlayerTwoDamas { get; init; }

    public int[,] CloneBoard()
    {
        return GameRecord.RowsToBoard(Board);
    }

    public static PositionSample FromSearchResult(
        int[,] board,
        int playerToMove,
        SearchResult result,
        string source = "search",
        string playerProfile = "",
        string opponentProfile = "",
        int ply = 0,
        int winner = 0,
        string resultText = "")
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        Move? bestMove = result.BestMove;
        return Create(
            board,
            playerToMove,
            source,
            sourceGameId: string.Empty,
            ply,
            playerProfile,
            opponentProfile,
            bestMove,
            result.BestEvaluation,
            result.CompletedDepth,
            result.Nodes,
            result.QuiescenceNodes,
            winner,
            resultText,
            gamePlyCount: 0,
            result.PrincipalVariation,
            result.TopMoves);
    }

    public static PositionSample FromGameMove(
        int[,] board,
        GameRecord game,
        GameMoveRecord move,
        string playerProfile,
        string opponentProfile)
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        if (move == null)
        {
            throw new ArgumentNullException(nameof(move));
        }

        return Create(
            board,
            move.Player,
            source: "game-database",
            sourceGameId: game.GameId,
            ply: move.Ply,
            playerProfile,
            opponentProfile,
            move.ToMove(),
            move.Evaluation,
            move.Depth,
            nodes: 0,
            quiescenceNodes: 0,
            game.Winner,
            game.ResultText,
            game.PlyCount,
            principalVariation: Array.Empty<Move>(),
            topMoves: Array.Empty<SearchCandidate>());
    }

    private static PositionSample Create(
        int[,] board,
        int playerToMove,
        string source,
        string sourceGameId,
        int ply,
        string playerProfile,
        string opponentProfile,
        Move? bestMove,
        int evaluation,
        int depth,
        long nodes,
        long quiescenceNodes,
        int winner,
        string resultText,
        int gamePlyCount,
        IEnumerable<Move> principalVariation,
        IEnumerable<SearchCandidate> topMoves)
    {
        MaterialCounts counts = CountMaterial(board);
        string bestMoveNotation = bestMove == null ? string.Empty : MoveNotation.Format(bestMove);
        return new PositionSample
        {
            Source = source,
            SourceGameId = sourceGameId,
            Ply = ply,
            PlayerToMove = playerToMove == -1 ? -1 : 1,
            PlayerProfile = playerProfile ?? string.Empty,
            OpponentProfile = opponentProfile ?? string.Empty,
            Board = GameRecord.BoardToRows(board),
            PositionKey = OpeningBook.ComputePositionKey(board, playerToMove),
            BestMove = bestMoveNotation,
            BestMoveKey = bestMove == null ? string.Empty : OpeningBook.ComputeMoveKey(bestMove),
            PrincipalVariation = principalVariation?.Select(MoveNotation.Format).ToList() ?? new List<string>(),
            TopMoves = topMoves?.Select(candidate => $"{candidate.Rank}. {MoveNotation.Format(candidate.Move)} {candidate.Score:+#;-#;0}").ToList() ?? new List<string>(),
            Evaluation = evaluation,
            SearchDepth = depth,
            Nodes = nodes,
            QuiescenceNodes = quiescenceNodes,
            Winner = winner,
            ResultText = resultText ?? string.Empty,
            GamePlyCount = gamePlyCount,
            PlayerOneMen = counts.PlayerOneMen,
            PlayerOneDamas = counts.PlayerOneDamas,
            PlayerTwoMen = counts.PlayerTwoMen,
            PlayerTwoDamas = counts.PlayerTwoDamas
        };
    }

    private static MaterialCounts CountMaterial(int[,] board)
    {
        MaterialCounts counts = new MaterialCounts();
        for (int row = 0; row < board.GetLength(0); row++)
        {
            for (int column = 0; column < board.GetLength(1); column++)
            {
                int piece = board[row, column];
                if (piece == 1)
                {
                    counts.PlayerOneMen++;
                }
                else if (piece == 2)
                {
                    counts.PlayerOneDamas++;
                }
                else if (piece == -1)
                {
                    counts.PlayerTwoMen++;
                }
                else if (piece == -2)
                {
                    counts.PlayerTwoDamas++;
                }
            }
        }

        return counts;
    }

    private sealed class MaterialCounts
    {
        public int PlayerOneMen { get; set; }
        public int PlayerOneDamas { get; set; }
        public int PlayerTwoMen { get; set; }
        public int PlayerTwoDamas { get; set; }
    }
}
