using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace WinDama.Core;

/// <summary>
/// Plays deterministic AI-vs-AI games between two evaluation profiles. This is
/// complementary to tactical benchmarks: benchmarks test known positions, while
/// tournaments test full-game behavior and profile robustness.
/// </summary>
public sealed class EvaluationTournamentRunner
{
    private readonly MoveGenerator moveGenerator;

    public EvaluationTournamentRunner()
        : this(new MoveGenerator())
    {
    }

    public EvaluationTournamentRunner(MoveGenerator moveGenerator)
    {
        this.moveGenerator = moveGenerator ?? throw new ArgumentNullException(nameof(moveGenerator));
    }

    public EvaluationTournamentSummary Run(EvaluationProfile profileA, EvaluationProfile profileB, EvaluationTournamentOptions? options = null)
    {
        if (profileA == null)
        {
            throw new ArgumentNullException(nameof(profileA));
        }

        if (profileB == null)
        {
            throw new ArgumentNullException(nameof(profileB));
        }

        options ??= new EvaluationTournamentOptions();
        int gamesPerColor = Math.Max(1, options.GamesPerColor);
        List<EvaluationTournamentGameResult> games = new List<EvaluationTournamentGameResult>();
        int gameNumber = 1;

        for (int i = 0; i < gamesPerColor; i++)
        {
            games.Add(PlayGame(gameNumber++, profileA, profileB, profileA, profileB, options));
            games.Add(PlayGame(gameNumber++, profileB, profileA, profileA, profileB, options));
        }

        return new EvaluationTournamentSummary
        {
            ProfileA = profileA.Name,
            ProfileB = profileB.Name,
            Games = games
        };
    }

    private EvaluationTournamentGameResult PlayGame(
        int gameNumber,
        EvaluationProfile playerOneProfile,
        EvaluationProfile playerTwoProfile,
        EvaluationProfile requestedA,
        EvaluationProfile requestedB,
        EvaluationTournamentOptions options)
    {
        GameController controller = new GameController(GameController.CreateInitialBoard(), 1, GameMode.HumanVsHuman, moveGenerator);
        SearchOptions searchOptions = options.ToSearchOptions();
        int maxPlies = Math.Max(1, options.MaxPliesPerGame);
        int quietLimit = Math.Max(1, options.MaxMovesWithoutCaptureOrPromotion);
        int quietPlies = 0;
        long nodes = 0;
        long qnodes = 0;
        long ttHits = 0;
        long ttCutoffs = 0;
        long killerCutoffs = 0;
        long historyUpdates = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
        StringBuilder log = new StringBuilder();
        List<GameMoveRecord> moveRecords = new List<GameMoveRecord>();
        string stopReason = string.Empty;

        for (int ply = 1; ply <= maxPlies; ply++)
        {
            if (controller.IsGameOver)
            {
                stopReason = controller.GetStatusMessage();
                break;
            }

            EvaluationProfile profileToMove = controller.CurrentPlayer == 1 ? playerOneProfile : playerTwoProfile;
            SearchEngine engine = new SearchEngine(moveGenerator, profileToMove.CreateEvaluation(), new Random(0));
            int[,] boardBefore = controller.Board;
            int movingPieceBefore = 0;

            SearchResult search = engine.FindBestMove(boardBefore, controller.CurrentPlayer, searchOptions);
            nodes += search.Nodes;
            qnodes += search.QuiescenceNodes;
            ttHits += search.TranspositionHits;
            ttCutoffs += search.TranspositionCutoffs;
            killerCutoffs += search.KillerMoveCutoffs;
            historyUpdates += search.HistoryHeuristicUpdates;

            if (search.BestMove == null)
            {
                stopReason = $"No legal move for {GameController.GetPlayerLabel(controller.CurrentPlayer)}.";
                break;
            }

            movingPieceBefore = boardBefore[search.BestMove.Start.Item1, search.BestMove.Start.Item2];
            GameTurnResult turn = controller.ApplyMove(search.BestMove);
            if (!turn.MoveApplied)
            {
                stopReason = $"Search returned illegal move: {MoveNotation.Format(search.BestMove)} ({turn.Message})";
                break;
            }

            AppendMove(log, ply, turn.PlayerBeforeMove, search.BestMove, search.BestEvaluation, search.CompletedDepth);
            moveRecords.Add(GameMoveRecord.FromMove(ply, turn.PlayerBeforeMove, search.BestMove, search.BestEvaluation, search.CompletedDepth));

            bool promoted = Math.Abs(movingPieceBefore) == 1 && Math.Abs(controller.Board[search.BestMove.End.Item1, search.BestMove.End.Item2]) == 2;
            quietPlies = search.BestMove.CapturedPieces.Count > 0 || promoted ? 0 : quietPlies + 1;
            if (quietPlies >= quietLimit)
            {
                stopReason = $"Draw by quiet-move limit ({quietLimit} plies).";
                controller.RestoreState(controller.Board, controller.CurrentPlayer, isGameOver: true, lastMove: controller.LastMove, history: controller.MoveHistory);
                break;
            }
        }

        stopwatch.Stop();

        if (string.IsNullOrWhiteSpace(stopReason))
        {
            stopReason = controller.IsGameOver
                ? controller.GetStatusMessage()
                : $"Draw by maximum ply limit ({maxPlies}).";
        }

        int winner = WinnerFromController(controller, stopReason);
        string winnerProfile = winner == 1
            ? playerOneProfile.Name
            : winner == -1
                ? playerTwoProfile.Name
                : "Draw";

        return new EvaluationTournamentGameResult
        {
            GameNumber = gameNumber,
            PlayerOneProfile = playerOneProfile.Name,
            PlayerTwoProfile = playerTwoProfile.Name,
            FirstRequestedProfile = requestedA.Name,
            SecondRequestedProfile = requestedB.Name,
            Winner = winner,
            WinnerProfile = winnerProfile,
            ResultText = ResultText(winner),
            StopReason = stopReason,
            PlyCount = CountLoggedPlies(log),
            Nodes = nodes,
            QuiescenceNodes = qnodes,
            TranspositionHits = ttHits,
            TranspositionCutoffs = ttCutoffs,
            KillerMoveCutoffs = killerCutoffs,
            HistoryHeuristicUpdates = historyUpdates,
            Elapsed = stopwatch.Elapsed,
            MoveLog = log.ToString().TrimEnd(),
            MoveRecords = moveRecords,
            FinalPlayerToMove = controller.CurrentPlayer
        };
    }

    private static int WinnerFromController(GameController controller, string stopReason)
    {
        string message = controller.GetStatusMessage();
        if (message.Contains("Player 1 wins", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (message.Contains("Player 2 wins", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        if (stopReason.Contains("Player 1 wins", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (stopReason.Contains("Player 2 wins", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        return 0;
    }

    private static string ResultText(int winner)
    {
        return winner switch
        {
            1 => "1-0",
            -1 => "0-1",
            _ => "1/2-1/2"
        };
    }

    private static void AppendMove(StringBuilder log, int ply, int player, Move move, int evaluation, int depth)
    {
        if (player == 1)
        {
            int fullMove = (ply + 1) / 2;
            log.Append(CultureInvariant(fullMove));
            log.Append(". ");
        }
        else if (log.Length == 0 || log[log.Length - 1] == '\n')
        {
            int fullMove = (ply + 1) / 2;
            log.Append(CultureInvariant(fullMove));
            log.Append("... ");
        }

        log.Append(MoveNotation.Format(move));
        log.Append($" {{{evaluation:+#;-#;0}/d{depth}}} ");

        if (player == -1)
        {
            log.AppendLine();
        }
    }

    private static string CultureInvariant(int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int CountLoggedPlies(StringBuilder log)
    {
        string text = log.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        // Every logged move contains a depth marker like /d3.
        return text.Split("/d", StringSplitOptions.None).Length - 1;
    }
}
