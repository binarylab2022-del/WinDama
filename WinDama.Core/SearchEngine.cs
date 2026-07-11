using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace WinDama.Core;

/// <summary>
/// UI-independent alpha-beta search for Spanish checkers positions.
/// It never reads WPF controls, never mutates the input board, and never shows UI.
/// </summary>
public sealed class SearchEngine
{
    private const int WinScore = 1_000_000;

    private readonly MoveGenerator moveGenerator;
    private readonly Evaluation evaluation;
    private readonly Random random;

    public SearchEngine()
        : this(new MoveGenerator(), new Evaluation(), new Random())
    {
    }

    public SearchEngine(MoveGenerator moveGenerator, Evaluation evaluation, Random? random = null)
    {
        this.moveGenerator = moveGenerator ?? throw new ArgumentNullException(nameof(moveGenerator));
        this.evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
        this.random = random ?? new Random();
    }

    public SearchResult FindBestMove(int[,] board, int player, SearchOptions options)
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        int maximumDepth = Math.Max(1, options.MaximumDepth);
        int forcedCaptureComparisonDepth = Math.Max(1, options.ForcedCaptureComparisonDepth);
        int tieBreakTolerance = Math.Max(0, options.TieBreakTolerance);
        int[,] rootBoard = (int[,])board.Clone();
        List<Move> legalMoves = OrderMoves(rootBoard, moveGenerator.GetPlayerCapturesOrMoves(rootBoard, player), transpositionBestMove: null);
        int captureMoveCount = legalMoves.Count(IsCapture);
        if (captureMoveCount > 0)
        {
            // Defensive enforcement: quiet moves are never legal once a capture exists.
            legalMoves = OrderMoves(rootBoard, legalMoves.Where(IsCapture).ToList(), transpositionBestMove: null);
            captureMoveCount = legalMoves.Count;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        TranspositionTable transpositionTable = options.UseTranspositionTable
            ? new TranspositionTable(options.TranspositionTableMaximumEntries)
            : new TranspositionTable(0);
        SearchHeuristics searchHeuristics = new SearchHeuristics();

        SearchResultBuilder result = new SearchResultBuilder
        {
            Mode = options.Mode,
            TimeBudgetMilliseconds = options.TimeLimitMilliseconds,
            BestEvaluation = evaluation.EvaluateBoard(rootBoard, player),
            LegalMoveCount = legalMoves.Count,
            CaptureMoveCount = captureMoveCount,
            Elapsed = TimeSpan.Zero,
            StopReason = "Search started",
            UsedQuiescenceSearch = options.UseQuiescenceSearch,
            MaxQuiescenceDepth = Math.Max(0, options.MaxQuiescenceDepth),
            KillerMoveCutoffs = 0,
            HistoryHeuristicUpdates = 0
        };

        ReportProgress(options, stopwatch, currentDepth: 0, completedDepth: 0, nodes: 0,
            bestEvaluation: result.BestEvaluation, bestMove: null, currentMove: null,
            legalMoveCount: legalMoves.Count, captureMoveCount: captureMoveCount,
            isForcedCaptureFastPath: false, timedOut: false,
            message: "Search started", force: true,
            transpositionTable: transpositionTable,
            heuristics: searchHeuristics);

        if (options.CancellationToken.IsCancellationRequested)
        {
            result.Elapsed = stopwatch.Elapsed;
            result.TimedOut = true;
            result.StopReason = "Search cancelled";
            result.UpdateSearchStats(transpositionTable, searchHeuristics);
            return result.ToResult();
        }

        if (legalMoves.Count == 0)
        {
            result.Elapsed = stopwatch.Elapsed;
            result.StopReason = "No legal moves";
            result.UpdateSearchStats(transpositionTable, searchHeuristics);
            return result.ToResult();
        }

        if (captureMoveCount == 1)
        {
            Move forcedMove = legalMoves[0];
            int[,] newBoard = MoveExecutor.ApplyMoveForSearch(rootBoard, forcedMove, moveGenerator);
            int score = evaluation.EvaluateBoard(newBoard, player);
            List<ScoredMove> scoredCaptures = new List<ScoredMove> { new ScoredMove(forcedMove, score) };

            result.BestMove = forcedMove;
            result.BestEvaluation = score;
            result.CompletedDepth = 0;
            result.Nodes = 1;
            result.Elapsed = stopwatch.Elapsed;
            result.NearBestMoveCount = 1;
            result.TopMoves = BuildTopMoves(scoredCaptures);
            result.PrincipalVariation = new List<Move> { CloneMove(forcedMove)! };
            result.UpdateSearchStats(transpositionTable, searchHeuristics);
            result.IsForcedCaptureFastPath = true;
            result.TimeBudgetMilliseconds = 0;
            result.StopReason = "Single Mandatory longest capture: minimax skipped";

            ReportProgress(options, stopwatch, currentDepth: 0, completedDepth: 0, nodes: result.Nodes,
                bestEvaluation: result.BestEvaluation, bestMove: result.BestMove, currentMove: result.BestMove,
                legalMoveCount: legalMoves.Count, captureMoveCount: captureMoveCount,
                isForcedCaptureFastPath: true, timedOut: false,
                message: result.StopReason, force: true,
                topMoves: result.TopMoves,
                principalVariation: result.PrincipalVariation,
                transpositionTable: transpositionTable,
            heuristics: searchHeuristics);

            return result.ToResult();
        }

        if (captureMoveCount > 1)
        {
            // There are several legal longest captures. Time is not charged, but
            // the alternatives must still be compared tactically instead of picked
            // by a shallow static evaluation.
            SearchState forcedState = new SearchState(stopwatch, null, options.CancellationToken, transpositionTable, searchHeuristics);
            long forcedProgressMilliseconds;
            List<ScoredMove> scoredCaptures = SearchRootMoves(
                rootBoard,
                player,
                legalMoves,
                forcedCaptureComparisonDepth,
                forcedState,
                options,
                stopwatch,
                currentDepthForProgress: forcedCaptureComparisonDepth,
                completedDepthForProgress: 0,
                lastProgressMilliseconds: out forcedProgressMilliseconds);

            result.Nodes += forcedState.Nodes;
            result.QuiescenceNodes += forcedState.QuiescenceNodes;
            result.Elapsed = stopwatch.Elapsed;
            result.TimedOut = forcedState.TimedOut;
            result.UpdateSearchStats(transpositionTable, searchHeuristics);

            if (scoredCaptures.Count > 0)
            {
                int bestScore = scoredCaptures.Max(m => m.Score);
                List<ScoredMove> nearBestMoves = scoredCaptures
                    .Where(m => bestScore - m.Score <= tieBreakTolerance)
                    .ToList();

                Move selectedMove = SelectMove(scoredCaptures, nearBestMoves, options.RandomizeNearBestMoves);
                result.BestMove = selectedMove;
                result.BestEvaluation = bestScore;
                result.CompletedDepth = forcedCaptureComparisonDepth;
                result.NearBestMoveCount = nearBestMoves.Count;
                result.TopMoves = BuildTopMoves(scoredCaptures);
                result.PrincipalVariation = BuildPrincipalVariation(rootBoard, player, selectedMove, transpositionTable, forcedCaptureComparisonDepth + 1);
                result.IsForcedCaptureFastPath = true;
                result.TimeBudgetMilliseconds = 0;
                result.StopReason = $"Multiple Mandatory longest captures compared at depth {forcedCaptureComparisonDepth}; game clock skipped";
            }
            else
            {
                result.StopReason = options.CancellationToken.IsCancellationRequested
                    ? "Search cancelled"
                    : "Mandatory captures available, but search was interrupted";
            }

            ReportProgress(options, stopwatch, currentDepth: forcedCaptureComparisonDepth, completedDepth: result.CompletedDepth,
                nodes: result.Nodes,
                bestEvaluation: result.BestEvaluation,
                bestMove: result.BestMove,
                currentMove: null,
                legalMoveCount: legalMoves.Count,
                captureMoveCount: captureMoveCount,
                isForcedCaptureFastPath: true,
                timedOut: result.TimedOut,
                message: result.StopReason,
                force: true,
                topMoves: result.TopMoves,
                principalVariation: result.PrincipalVariation,
                transpositionTable: transpositionTable,
            heuristics: searchHeuristics);

            return result.ToResult();
        }

        int firstDepth = options.Iterative ? 1 : maximumDepth;
        long lastProgressMilliseconds = -1;

        for (int depth = firstDepth; depth <= maximumDepth; depth++)
        {
            SearchState state = new SearchState(stopwatch, options.TimeLimitMilliseconds, options.CancellationToken, transpositionTable, searchHeuristics);
            List<ScoredMove> scoredMoves = SearchRootMoves(
                rootBoard,
                player,
                legalMoves,
                depth,
                state,
                options,
                stopwatch,
                currentDepthForProgress: depth,
                completedDepthForProgress: result.CompletedDepth,
                lastProgressMilliseconds: out lastProgressMilliseconds,
                accumulatedNodeOffset: result.Nodes,
                captureMoveCount: captureMoveCount,
                isForcedCaptureFastPath: false);

            result.Nodes += state.Nodes;
            result.QuiescenceNodes += state.QuiescenceNodes;
            result.Elapsed = stopwatch.Elapsed;
            result.TimedOut = state.TimedOut;
            result.UpdateSearchStats(transpositionTable, searchHeuristics);

            // Keep the last complete depth. If time expires before any depth is
            // complete, still keep the best partial result so the UI has a move.
            if (scoredMoves.Count > 0 && (!state.TimedOut || result.CompletedDepth == 0))
            {
                int bestScore = scoredMoves.Max(m => m.Score);
                List<ScoredMove> nearBestMoves = scoredMoves
                    .Where(m => bestScore - m.Score <= tieBreakTolerance)
                    .ToList();

                Move selectedMove = SelectMove(scoredMoves, nearBestMoves, options.RandomizeNearBestMoves);

                result.BestMove = selectedMove;
                result.BestEvaluation = bestScore;
                result.CompletedDepth = depth;
                result.NearBestMoveCount = nearBestMoves.Count;
                result.TopMoves = BuildTopMoves(scoredMoves);
                result.PrincipalVariation = BuildPrincipalVariation(rootBoard, player, selectedMove, transpositionTable, depth + 1);
                result.StopReason = state.TimedOut ? "Time limit reached before full depth completed" : $"Depth {depth} completed";
            }

            ReportProgress(options, stopwatch, currentDepth: depth, completedDepth: result.CompletedDepth,
                nodes: result.Nodes,
                bestEvaluation: result.BestEvaluation,
                bestMove: result.BestMove,
                currentMove: null,
                legalMoveCount: legalMoves.Count,
                captureMoveCount: captureMoveCount,
                isForcedCaptureFastPath: false,
                timedOut: state.TimedOut,
                message: result.StopReason,
                force: true,
                topMoves: result.TopMoves,
                principalVariation: result.PrincipalVariation,
                transpositionTable: transpositionTable,
            heuristics: searchHeuristics);

            if (!options.Iterative || state.TimedOut || state.IsTimeUp)
            {
                break;
            }
        }

        result.Elapsed = stopwatch.Elapsed;
        if (options.CancellationToken.IsCancellationRequested)
        {
            result.TimedOut = true;
            result.StopReason = "Search cancelled";
        }
        else if (string.IsNullOrWhiteSpace(result.StopReason) || result.StopReason == "Search started")
        {
            result.StopReason = result.TimedOut ? "Time limit reached" : "Search completed";
        }

        result.UpdateSearchStats(transpositionTable, searchHeuristics);
        return result.ToResult();
    }

    private List<ScoredMove> SearchRootMoves(
        int[,] rootBoard,
        int player,
        List<Move> legalMoves,
        int depth,
        SearchState state,
        SearchOptions options,
        Stopwatch stopwatch,
        int currentDepthForProgress,
        int completedDepthForProgress,
        out long lastProgressMilliseconds,
        int accumulatedNodeOffset = 0,
        int? captureMoveCount = null,
        bool isForcedCaptureFastPath = false)
    {
        lastProgressMilliseconds = -1;
        List<ScoredMove> scoredMoves = new List<ScoredMove>();
        int capCount = captureMoveCount ?? legalMoves.Count(IsCapture);
        ulong rootHash = state.TranspositionTable.ComputeHash(rootBoard, player, player);
        Move? rootTableBestMove = state.TranspositionTable.TryGetBestMove(rootHash);
        legalMoves = OrderMovesWithHeuristics(rootBoard, legalMoves, rootTableBestMove, state, depth, options);

        foreach (Move move in legalMoves)
        {
            if (state.IsTimeUp)
            {
                state.TimedOut = true;
                break;
            }

            int[,] newBoard = MoveExecutor.ApplyMoveForSearch(rootBoard, move, moveGenerator);

            // Keep the earlier WPF depth semantics: depth N means N plies are
            // searched after each candidate root move.
            int score = Minimax(
                newBoard,
                depth,
                isMaximizingPlayer: false,
                alpha: int.MinValue,
                beta: int.MaxValue,
                currentPlayer: -player,
                rootPlayer: player,
                state: state,
                options: options);

            scoredMoves.Add(new ScoredMove(move, score));

            ScoredMove partialBest = scoredMoves.OrderByDescending(m => m.Score).First();
            IReadOnlyList<SearchCandidate> topMoves = BuildTopMoves(scoredMoves);
            IReadOnlyList<Move> pv = BuildPrincipalVariation(rootBoard, player, partialBest.Move, state.TranspositionTable, depth + 1);
            MaybeReportProgress(options, stopwatch, ref lastProgressMilliseconds,
                currentDepth: currentDepthForProgress,
                completedDepth: completedDepthForProgress,
                nodes: accumulatedNodeOffset + state.Nodes,
                bestEvaluation: partialBest.Score,
                bestMove: partialBest.Move,
                currentMove: move,
                legalMoveCount: legalMoves.Count,
                captureMoveCount: capCount,
                isForcedCaptureFastPath: isForcedCaptureFastPath,
                timedOut: state.TimedOut,
                message: isForcedCaptureFastPath
                    ? $"Comparing Mandatory captures at depth {depth}"
                    : $"Searching depth {depth}",
                quiescenceNodes: state.QuiescenceNodes,
                topMoves: topMoves,
                principalVariation: pv,
                transpositionTable: state.TranspositionTable,
                heuristics: state.Heuristics);

            if (state.IsTimeUp)
            {
                state.TimedOut = true;
                break;
            }
        }

        return scoredMoves;
    }

    private int Minimax(
        int[,] board,
        int depth,
        bool isMaximizingPlayer,
        int alpha,
        int beta,
        int currentPlayer,
        int rootPlayer,
        SearchState state,
        SearchOptions options)
    {
        if (state.IsTimeUp)
        {
            state.TimedOut = true;
            return evaluation.EvaluateBoard(board, rootPlayer);
        }

        state.Nodes++;

        int originalAlpha = alpha;
        int originalBeta = beta;
        ulong hash = state.TranspositionTable.ComputeHash(board, currentPlayer, rootPlayer);

        if (state.TranspositionTable.TryProbe(hash, depth, alpha, beta, out int cachedScore, out Move? tableBestMove))
        {
            return cachedScore;
        }

        List<Move> legalMoves = OrderMovesWithHeuristics(board, moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer), tableBestMove, state, depth, options);

        if (legalMoves.Count == 0)
        {
            int terminalScore = currentPlayer == rootPlayer
                ? -WinScore - depth
                : WinScore + depth;
            state.TranspositionTable.Store(hash, depth, terminalScore, TranspositionBound.Exact, null);
            return terminalScore;
        }

        if (depth == 0)
        {
            int leafScore = options.UseQuiescenceSearch
                ? Quiescence(
                    board,
                    currentPlayer,
                    rootPlayer,
                    state,
                    alpha,
                    beta,
                    isMaximizingPlayer,
                    Math.Max(0, options.MaxQuiescenceDepth),
                    options)
                : evaluation.EvaluateBoard(board, rootPlayer);

            state.TranspositionTable.Store(hash, depth, leafScore, TranspositionBound.Exact, null);
            return leafScore;
        }

        int bestScore = isMaximizingPlayer ? int.MinValue : int.MaxValue;
        Move? bestMove = null;
        bool searchedAtLeastOneMove = false;

        foreach (Move move in legalMoves)
        {
            if (state.IsTimeUp)
            {
                state.TimedOut = true;
                break;
            }

            int[,] newBoard = MoveExecutor.ApplyMoveForSearch(board, move, moveGenerator);
            int score = Minimax(
                newBoard,
                depth - 1,
                !isMaximizingPlayer,
                alpha,
                beta,
                -currentPlayer,
                rootPlayer,
                state,
                options);

            searchedAtLeastOneMove = true;

            if (isMaximizingPlayer)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, score);
            }
            else
            {
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                beta = Math.Min(beta, score);
            }

            if (beta <= alpha)
            {
                state.Heuristics.RecordCutoff(move, depth, options);
                break;
            }
        }

        if (!searchedAtLeastOneMove)
        {
            return evaluation.EvaluateBoard(board, rootPlayer);
        }

        if (!state.TimedOut)
        {
            TranspositionBound bound;
            if (bestScore <= originalAlpha)
            {
                bound = TranspositionBound.UpperBound;
            }
            else if (bestScore >= originalBeta)
            {
                bound = TranspositionBound.LowerBound;
            }
            else
            {
                bound = TranspositionBound.Exact;
            }

            state.TranspositionTable.Store(hash, depth, bestScore, bound, bestMove);
        }

        return bestScore;
    }

    private int Quiescence(
        int[,] board,
        int currentPlayer,
        int rootPlayer,
        SearchState state,
        int alpha,
        int beta,
        bool isMaximizingPlayer,
        int remainingQuiescenceDepth,
        SearchOptions options)
    {
        if (state.IsTimeUp)
        {
            state.TimedOut = true;
            return evaluation.EvaluateBoard(board, rootPlayer);
        }

        state.Nodes++;
        state.QuiescenceNodes++;

        List<Move> legalMoves = moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer);
        if (legalMoves.Count == 0)
        {
            return currentPlayer == rootPlayer
                ? -WinScore - remainingQuiescenceDepth
                : WinScore + remainingQuiescenceDepth;
        }

        List<Move> captureMoves = OrderMovesWithHeuristics(
            board,
            legalMoves.Where(IsCapture).ToList(),
            transpositionBestMove: null,
            state: state,
            depth: remainingQuiescenceDepth,
            options: options);

        if (captureMoves.Count == 0 || remainingQuiescenceDepth <= 0)
        {
            return evaluation.EvaluateBoard(board, rootPlayer);
        }

        if (isMaximizingPlayer)
        {
            int bestScore = int.MinValue;
            foreach (Move move in captureMoves)
            {
                if (state.IsTimeUp)
                {
                    state.TimedOut = true;
                    break;
                }

                int[,] newBoard = MoveExecutor.ApplyMoveForSearch(board, move, moveGenerator);
                int score = Quiescence(
                    newBoard,
                    -currentPlayer,
                    rootPlayer,
                    state,
                    alpha,
                    beta,
                    isMaximizingPlayer: false,
                    remainingQuiescenceDepth - 1,
                    options);

                if (score > bestScore)
                {
                    bestScore = score;
                }

                alpha = Math.Max(alpha, score);
                if (beta <= alpha)
                {
                    state.Heuristics.RecordCutoff(move, remainingQuiescenceDepth, options);
                    break;
                }
            }

            return bestScore == int.MinValue ? evaluation.EvaluateBoard(board, rootPlayer) : bestScore;
        }
        else
        {
            int bestScore = int.MaxValue;
            foreach (Move move in captureMoves)
            {
                if (state.IsTimeUp)
                {
                    state.TimedOut = true;
                    break;
                }

                int[,] newBoard = MoveExecutor.ApplyMoveForSearch(board, move, moveGenerator);
                int score = Quiescence(
                    newBoard,
                    -currentPlayer,
                    rootPlayer,
                    state,
                    alpha,
                    beta,
                    isMaximizingPlayer: true,
                    remainingQuiescenceDepth - 1,
                    options);

                if (score < bestScore)
                {
                    bestScore = score;
                }

                beta = Math.Min(beta, score);
                if (beta <= alpha)
                {
                    state.Heuristics.RecordCutoff(move, remainingQuiescenceDepth, options);
                    break;
                }
            }

            return bestScore == int.MaxValue ? evaluation.EvaluateBoard(board, rootPlayer) : bestScore;
        }
    }

    private List<Move> OrderMoves(int[,] board, List<Move> moves, Move? transpositionBestMove = null)
    {
        return OrderMovesInternal(board, moves, transpositionBestMove, state: null, depth: 0, options: null);
    }

    private List<Move> OrderMovesWithHeuristics(
        int[,] board,
        List<Move> moves,
        Move? transpositionBestMove,
        SearchState state,
        int depth,
        SearchOptions options)
    {
        return OrderMovesInternal(board, moves, transpositionBestMove, state, depth, options);
    }

    private List<Move> OrderMovesInternal(
        int[,] board,
        List<Move> moves,
        Move? transpositionBestMove,
        SearchState? state,
        int depth,
        SearchOptions? options)
    {
        if (moves == null || moves.Count <= 1)
        {
            return moves ?? new List<Move>();
        }

        return moves
            .Select((move, index) => new OrderedMove(move, ScoreMoveForOrdering(board, move, transpositionBestMove, state, depth, options), index))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Move.Start.Item1)
            .ThenBy(item => item.Move.Start.Item2)
            .ThenBy(item => item.Move.End.Item1)
            .ThenBy(item => item.Move.End.Item2)
            .ThenBy(item => item.OriginalIndex)
            .Select(item => item.Move)
            .ToList();
    }

    private int ScoreMoveForOrdering(
        int[,] board,
        Move move,
        Move? transpositionBestMove,
        SearchState? state = null,
        int depth = 0,
        SearchOptions? options = null)
    {
        int score = 0;
        int movingPiece = board[move.Start.Item1, move.Start.Item2];
        int movingPlayer = movingPiece >= 0 ? 1 : -1;
        bool isDama = Math.Abs(movingPiece) == 2;
        bool isCapture = IsCapture(move);

        // 1. Stored TT best move: this is the strongest move-ordering hint.
        if (transpositionBestMove != null && SameMove(move, transpositionBestMove))
        {
            score += 10_000_000;
        }

        // 2. Killer move heuristic. A quiet move that caused a beta cutoff at
        // the same remaining depth is often good in sibling nodes too.
        if (!isCapture
            && options?.UseKillerMoves == true
            && state?.Heuristics.IsKillerMove(depth, move) == true)
        {
            score += 850_000;
        }

        // 3. History heuristic. This is lower priority than TT/killer/captures,
        // but it helps order quiet alternatives after a few cutoffs have been seen.
        if (options?.UseHistoryHeuristic == true && state != null)
        {
            score += Math.Min(350_000, state.Heuristics.GetHistoryScore(move) * 12);
        }

        // 4. Capture quality: longest and most valuable captures first.
        if (isCapture)
        {
            score += 1_000_000;
            score += move.CapturedPieces.Count * 100_000;
            score += CapturedMaterialValue(board, move) * 1_000;

            // Captures ending safely or with promotion are often tactically decisive.
            if (MovePromotes(board, move))
            {
                score += 80_000;
            }
        }
        else if (MovePromotes(board, move))
        {
            // 5. Quiet promotion moves should be searched very early.
            score += 300_000;
        }

        // 6. Dama activity: prefer active long diagonal moves and central landing.
        if (isDama)
        {
            int distance = Math.Max(
                Math.Abs(move.End.Item1 - move.Start.Item1),
                Math.Abs(move.End.Item2 - move.Start.Item2));
            score += 40_000;
            score += distance * 1_500;
        }

        score += LandingSquareScore(move.End.Item1, move.End.Item2, isDama);

        // 7. Safety: prefer moves that do not leave the moved piece immediately capturable.
        // This is intentionally a move-ordering hint only. It does not change evaluation.
        if (LeavesMovedPieceImmediatelyCapturable(board, move, movingPlayer))
        {
            score -= isCapture ? 25_000 : 70_000;
        }
        else
        {
            score += isCapture ? 20_000 : 10_000;
        }

        // 8. Small deterministic tie-breaker: avoid unstable random ordering in tests.
        score += (7 - move.End.Item1) * 4;
        score += move.End.Item2;
        return score;
    }

    private static int CapturedMaterialValue(int[,] board, Move move)
    {
        int value = 0;
        foreach ((int row, int column) in move.CapturedPieces)
        {
            int capturedPiece = Math.Abs(board[row, column]);
            value += capturedPiece == 2 ? 500 : capturedPiece == 1 ? 100 : 0;
        }

        return value;
    }

    private static bool MovePromotes(int[,] board, Move move)
    {
        int piece = board[move.Start.Item1, move.Start.Item2];
        return piece == 1 && move.End.Item1 == 0
            || piece == -1 && move.End.Item1 == 7;
    }

    private static int LandingSquareScore(int row, int column, bool isDama)
    {
        int score = 0;

        if (row >= 2 && row <= 5 && column >= 2 && column <= 5)
        {
            score += isDama ? 18_000 : 8_000;
        }

        if (row == 0 || row == 7 || column == 0 || column == 7)
        {
            score += isDama ? 4_000 : 9_000;
        }

        // Prefer squares with more diagonal freedom. This helps Dama mobility
        // and also gives men more flexible future movement.
        int centrality = 7 - (Math.Abs(3 - row) + Math.Abs(4 - column));
        score += centrality * (isDama ? 900 : 350);
        return score;
    }

    private bool LeavesMovedPieceImmediatelyCapturable(int[,] board, Move move, int movingPlayer)
    {
        int[,] afterMove = MoveExecutor.ApplyMoveForSearch(board, move, moveGenerator);
        return moveGenerator.GetPlayerCapturesOnly(afterMove, -movingPlayer)
            .Any(reply => reply.CapturedPieces.Any(square => square == move.End));
    }

    private IReadOnlyList<Move> BuildPrincipalVariation(int[,] rootBoard, int rootPlayer, Move? rootMove, TranspositionTable table, int maxPlies)
    {
        List<Move> variation = new List<Move>();
        if (rootMove == null || maxPlies <= 0)
        {
            return variation;
        }

        int[,] pvBoard = (int[,])rootBoard.Clone();
        int sideToMove = rootPlayer;
        Move? nextMove = rootMove;

        for (int ply = 0; ply < maxPlies && nextMove != null; ply++)
        {
            List<Move> legalMoves = moveGenerator.GetPlayerCapturesOrMoves(pvBoard, sideToMove);
            Move? legalMove = legalMoves.FirstOrDefault(move => SameMove(move, nextMove));
            if (legalMove == null)
            {
                break;
            }

            variation.Add(CloneMove(legalMove)!);
            pvBoard = MoveExecutor.ApplyMoveForSearch(pvBoard, legalMove, moveGenerator);
            sideToMove = -sideToMove;

            ulong hash = table.ComputeHash(pvBoard, sideToMove, rootPlayer);
            nextMove = table.TryGetBestMove(hash);
        }

        return variation;
    }

    private static IReadOnlyList<SearchCandidate> BuildTopMoves(IEnumerable<ScoredMove> scoredMoves, int limit = 5)
    {
        return scoredMoves
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Move.Start.Item1)
            .ThenBy(item => item.Move.Start.Item2)
            .ThenBy(item => item.Move.End.Item1)
            .ThenBy(item => item.Move.End.Item2)
            .Take(Math.Max(1, limit))
            .Select((item, index) => new SearchCandidate(CloneMove(item.Move)!, item.Score, index + 1))
            .ToList();
    }

    private Move SelectMove(List<ScoredMove> scoredMoves, List<ScoredMove> nearBestMoves, bool randomize)
    {
        if (nearBestMoves.Count == 0)
        {
            return CloneMove(scoredMoves.OrderByDescending(m => m.Score).First().Move)!;
        }

        if (!randomize || nearBestMoves.Count == 1)
        {
            return CloneMove(nearBestMoves.OrderByDescending(m => m.Score).First().Move)!;
        }

        return CloneMove(nearBestMoves[random.Next(nearBestMoves.Count)].Move)!;
    }

    private static bool IsCapture(Move move)
    {
        return move.CapturedPieces != null && move.CapturedPieces.Count > 0;
    }

    private static bool SameMove(Move left, Move right)
    {
        return left.Start == right.Start
            && left.End == right.End
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }

    private static Move? CloneMove(Move? move)
    {
        return move == null
            ? null
            : new Move(move.Start, move.End, new List<(int, int)>(move.CapturedPieces));
    }

    private static double CalculateNodesPerSecond(int nodes, TimeSpan elapsed)
    {
        return elapsed.TotalSeconds <= 0.0001 ? nodes : nodes / elapsed.TotalSeconds;
    }

    private static void MaybeReportProgress(
        SearchOptions options,
        Stopwatch stopwatch,
        ref long lastProgressMilliseconds,
        int currentDepth,
        int completedDepth,
        int nodes,
        int bestEvaluation,
        Move? bestMove,
        Move? currentMove,
        int legalMoveCount,
        int captureMoveCount,
        bool isForcedCaptureFastPath,
        bool timedOut,
        string message,
        int quiescenceNodes = 0,
        IReadOnlyList<SearchCandidate>? topMoves = null,
        IReadOnlyList<Move>? principalVariation = null,
        TranspositionTable? transpositionTable = null,
        SearchHeuristics? heuristics = null)
    {
        if (options.Progress == null)
        {
            return;
        }

        int interval = Math.Max(50, options.ProgressIntervalMilliseconds);
        long elapsed = stopwatch.ElapsedMilliseconds;
        if (lastProgressMilliseconds >= 0 && elapsed - lastProgressMilliseconds < interval)
        {
            return;
        }

        lastProgressMilliseconds = elapsed;
        ReportProgress(options, stopwatch, currentDepth, completedDepth, nodes, bestEvaluation, bestMove, currentMove,
            legalMoveCount, captureMoveCount, isForcedCaptureFastPath, timedOut, message, force: true,
            quiescenceNodes: quiescenceNodes,
            topMoves: topMoves,
            principalVariation: principalVariation,
            transpositionTable: transpositionTable,
            heuristics: heuristics);
    }

    private static void ReportProgress(
        SearchOptions options,
        Stopwatch stopwatch,
        int currentDepth,
        int completedDepth,
        int nodes,
        int bestEvaluation,
        Move? bestMove,
        Move? currentMove,
        int legalMoveCount,
        int captureMoveCount,
        bool isForcedCaptureFastPath,
        bool timedOut,
        string message,
        bool force,
        int quiescenceNodes = 0,
        IReadOnlyList<SearchCandidate>? topMoves = null,
        IReadOnlyList<Move>? principalVariation = null,
        TranspositionTable? transpositionTable = null,
        SearchHeuristics? heuristics = null)
    {
        if (options.Progress == null)
        {
            return;
        }

        TimeSpan elapsed = stopwatch.Elapsed;
        options.Progress(new SearchProgress
        {
            CurrentDepth = currentDepth,
            CompletedDepth = completedDepth,
            Nodes = nodes,
            QuiescenceNodes = quiescenceNodes,
            NodesPerSecond = CalculateNodesPerSecond(nodes, elapsed),
            BestEvaluation = bestEvaluation,
            BestMove = CloneMove(bestMove),
            CurrentMove = CloneMove(currentMove),
            Elapsed = elapsed,
            LegalMoveCount = legalMoveCount,
            CaptureMoveCount = captureMoveCount,
            IsForcedCaptureFastPath = isForcedCaptureFastPath,
            TimedOut = timedOut,
            Message = message,
            TopMoves = topMoves ?? Array.Empty<SearchCandidate>(),
            PrincipalVariation = principalVariation ?? Array.Empty<Move>(),
            TranspositionHits = transpositionTable?.Hits ?? 0,
            TranspositionStores = transpositionTable?.Stores ?? 0,
            TranspositionCutoffs = transpositionTable?.Cutoffs ?? 0,
            TranspositionEntries = transpositionTable?.Count ?? 0,
            UsedQuiescenceSearch = options.UseQuiescenceSearch,
            MaxQuiescenceDepth = Math.Max(0, options.MaxQuiescenceDepth),
            KillerMoveCutoffs = heuristics?.KillerMoveCutoffs ?? 0,
            HistoryHeuristicUpdates = heuristics?.HistoryHeuristicUpdates ?? 0
        });
    }

    private sealed class SearchHeuristics
    {
        private readonly Dictionary<int, List<Move>> killerMovesByDepth = new();
        private readonly Dictionary<string, int> historyScores = new();

        public long KillerMoveCutoffs { get; private set; }
        public long HistoryHeuristicUpdates { get; private set; }

        public bool IsKillerMove(int depth, Move move)
        {
            return killerMovesByDepth.TryGetValue(Math.Max(0, depth), out List<Move>? killers)
                && killers.Any(killer => SameMove(killer, move));
        }

        public int GetHistoryScore(Move move)
        {
            return historyScores.TryGetValue(GetMoveHistoryKey(move), out int score) ? score : 0;
        }

        public void RecordCutoff(Move move, int depth, SearchOptions options)
        {
            int safeDepth = Math.Max(0, depth);

            if (options.UseHistoryHeuristic)
            {
                string key = GetMoveHistoryKey(move);
                int bonus = Math.Max(1, safeDepth * safeDepth);
                int existing = historyScores.TryGetValue(key, out int current) ? current : 0;
                historyScores[key] = Math.Min(1_000_000, existing + bonus);
                HistoryHeuristicUpdates++;
            }

            if (!options.UseKillerMoves || IsCapture(move))
            {
                return;
            }

            KillerMoveCutoffs++;

            int maxKillers = Math.Max(1, options.KillerMovesPerDepth);
            if (!killerMovesByDepth.TryGetValue(safeDepth, out List<Move>? killers))
            {
                killers = new List<Move>();
                killerMovesByDepth[safeDepth] = killers;
            }

            int existingIndex = killers.FindIndex(killer => SameMove(killer, move));
            if (existingIndex >= 0)
            {
                Move existing = killers[existingIndex];
                killers.RemoveAt(existingIndex);
                killers.Insert(0, existing);
            }
            else
            {
                killers.Insert(0, CloneMove(move)!);
            }

            while (killers.Count > maxKillers)
            {
                killers.RemoveAt(killers.Count - 1);
            }
        }

        private static string GetMoveHistoryKey(Move move)
        {
            string captures = move.CapturedPieces == null || move.CapturedPieces.Count == 0
                ? string.Empty
                : string.Join(";", move.CapturedPieces.Select(square => $"{square.Item1},{square.Item2}"));
            return $"{move.Start.Item1},{move.Start.Item2}>{move.End.Item1},{move.End.Item2}|{captures}";
        }
    }

    private sealed class SearchState
    {
        public SearchState(Stopwatch stopwatch, int? timeLimitMilliseconds, CancellationToken cancellationToken, TranspositionTable transpositionTable, SearchHeuristics heuristics)
        {
            Stopwatch = stopwatch;
            TimeLimitMilliseconds = timeLimitMilliseconds;
            CancellationToken = cancellationToken;
            TranspositionTable = transpositionTable;
            Heuristics = heuristics;
        }

        public Stopwatch Stopwatch { get; }
        public int? TimeLimitMilliseconds { get; }
        public CancellationToken CancellationToken { get; }
        public TranspositionTable TranspositionTable { get; }
        public SearchHeuristics Heuristics { get; }
        public int Nodes { get; set; }
        public int QuiescenceNodes { get; set; }
        public bool TimedOut { get; set; }

        public bool IsTimeUp => CancellationToken.IsCancellationRequested
            || (TimeLimitMilliseconds.HasValue && Stopwatch.ElapsedMilliseconds >= TimeLimitMilliseconds.Value);
    }

    private readonly record struct OrderedMove(Move Move, int Score, int OriginalIndex);

    private readonly record struct ScoredMove(Move Move, int Score);

    private sealed class SearchResultBuilder
    {
        public Move? BestMove { get; set; }
        public int BestEvaluation { get; set; }
        public int CompletedDepth { get; set; }
        public int Nodes { get; set; }
        public int QuiescenceNodes { get; set; }
        public TimeSpan Elapsed { get; set; }
        public bool TimedOut { get; set; }
        public int LegalMoveCount { get; set; }
        public int CaptureMoveCount { get; set; }
        public int NearBestMoveCount { get; set; }
        public int? TimeBudgetMilliseconds { get; set; }
        public SearchMode Mode { get; set; }
        public bool IsForcedCaptureFastPath { get; set; }
        public string StopReason { get; set; } = string.Empty;
        public IReadOnlyList<SearchCandidate> TopMoves { get; set; } = Array.Empty<SearchCandidate>();
        public IReadOnlyList<Move> PrincipalVariation { get; set; } = Array.Empty<Move>();
        public long TranspositionHits { get; set; }
        public long TranspositionStores { get; set; }
        public long TranspositionCutoffs { get; set; }
        public int TranspositionEntries { get; set; }
        public bool UsedQuiescenceSearch { get; set; }
        public int MaxQuiescenceDepth { get; set; }

        public long KillerMoveCutoffs { get; set; }
        public long HistoryHeuristicUpdates { get; set; }

        public void UpdateSearchStats(TranspositionTable table, SearchHeuristics heuristics)
        {
            TranspositionHits = table.Hits;
            TranspositionStores = table.Stores;
            TranspositionCutoffs = table.Cutoffs;
            TranspositionEntries = table.Count;
            KillerMoveCutoffs = heuristics.KillerMoveCutoffs;
            HistoryHeuristicUpdates = heuristics.HistoryHeuristicUpdates;
        }

        public SearchResult ToResult()
        {
            return new SearchResult
            {
                BestMove = CloneMove(BestMove),
                BestEvaluation = BestEvaluation,
                CompletedDepth = CompletedDepth,
                Nodes = Nodes,
                QuiescenceNodes = QuiescenceNodes,
                Elapsed = Elapsed,
                TimedOut = TimedOut,
                LegalMoveCount = LegalMoveCount,
                CaptureMoveCount = CaptureMoveCount,
                NearBestMoveCount = NearBestMoveCount,
                TimeBudgetMilliseconds = TimeBudgetMilliseconds,
                NodesPerSecond = CalculateNodesPerSecond(Nodes, Elapsed),
                IsForcedCaptureFastPath = IsForcedCaptureFastPath,
                StopReason = StopReason,
                Mode = Mode,
                TopMoves = TopMoves,
                PrincipalVariation = PrincipalVariation,
                TranspositionHits = TranspositionHits,
                TranspositionStores = TranspositionStores,
                TranspositionCutoffs = TranspositionCutoffs,
                TranspositionEntries = TranspositionEntries,
                UsedQuiescenceSearch = UsedQuiescenceSearch,
                MaxQuiescenceDepth = MaxQuiescenceDepth,
                KillerMoveCutoffs = KillerMoveCutoffs,
                HistoryHeuristicUpdates = HistoryHeuristicUpdates
            };
        }
    }
}
