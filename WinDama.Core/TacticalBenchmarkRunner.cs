using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WinDama.Core;

/// <summary>
/// Runs the engine against saved tactical JSON positions using one or more
/// evaluation profiles. This provides a repeatable way to tune weights instead
/// of relying only on subjective Human-vs-AI play.
/// </summary>
public sealed class TacticalBenchmarkRunner
{
    private readonly MoveGenerator moveGenerator;

    public TacticalBenchmarkRunner()
        : this(new MoveGenerator())
    {
    }

    public TacticalBenchmarkRunner(MoveGenerator moveGenerator)
    {
        this.moveGenerator = moveGenerator ?? throw new ArgumentNullException(nameof(moveGenerator));
    }

    public TacticalBenchmarkSummary Run(string positionsDirectory, string profilesDirectory, TacticalBenchmarkOptions? options = null)
    {
        IReadOnlyList<EvaluationProfile> profiles = EvaluationProfileStore.LoadProfiles(profilesDirectory);
        return Run(positionsDirectory, profiles, options);
    }

    public TacticalBenchmarkSummary Run(string positionsDirectory, IReadOnlyList<EvaluationProfile> profiles, TacticalBenchmarkOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(positionsDirectory))
        {
            throw new ArgumentException("A tactical-position directory is required.", nameof(positionsDirectory));
        }

        if (!Directory.Exists(positionsDirectory))
        {
            throw new DirectoryNotFoundException($"Tactical-position directory was not found: {positionsDirectory}");
        }

        if (profiles == null || profiles.Count == 0)
        {
            profiles = new List<EvaluationProfile> { new EvaluationProfile("default", EvaluationWeights.Default) };
        }

        options ??= new TacticalBenchmarkOptions();
        string[] positionFiles = Directory
            .GetFiles(positionsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaximumPositions < 1 ? int.MaxValue : options.MaximumPositions)
            .ToArray();

        List<TacticalBenchmarkPositionResult> results = new List<TacticalBenchmarkPositionResult>();

        foreach (string positionFile in positionFiles)
        {
            TestPosition position = TestPositionStore.Load(positionFile);
            int[,] board = position.ToBoard();

            foreach (EvaluationProfile profile in profiles)
            {
                SearchEngine searchEngine = new SearchEngine(moveGenerator, profile.CreateEvaluation(), new Random(0));
                SearchResult searchResult = searchEngine.FindBestMove(board, position.CurrentPlayer, options.ToSearchOptions());
                TacticalBenchmarkScore score = TacticalBenchmarkScoring.Score(position, searchResult);

                results.Add(new TacticalBenchmarkPositionResult
                {
                    PositionName = position.Name,
                    PositionFile = Path.GetFileName(positionFile),
                    ProfileName = profile.Name,
                    EvaluatorType = profile.EvaluatorType,
                    SideToMove = position.CurrentPlayer,
                    Evaluation = searchResult.BestEvaluation,
                    CompletedDepth = searchResult.CompletedDepth,
                    Nodes = searchResult.Nodes,
                    QuiescenceNodes = searchResult.QuiescenceNodes,
                    NodesPerSecond = searchResult.NodesPerSecond,
                    Elapsed = searchResult.Elapsed,
                    LegalMoveCount = searchResult.LegalMoveCount,
                    CaptureMoveCount = searchResult.CaptureMoveCount,
                    BestMove = MoveNotation.Format(searchResult.BestMove),
                    PrincipalVariation = MoveNotation.FormatLine(searchResult.PrincipalVariation),
                    TopMoves = MoveNotation.FormatTopMoves(searchResult.TopMoves),
                    StopReason = searchResult.StopReason,
                    Score = score.Score,
                    MaximumScore = score.MaximumScore,
                    ExpectedBestMove = FormatExpectedBestMoves(position),
                    ExpectedEvaluationSign = position.ExpectedEvaluationSign,
                    ExpectedLegalMoveCount = position.ExpectedLegalMoveCount,
                    ExpectedCaptureMoveCount = position.ExpectedCaptureMoveCount,
                    ExpectedMinBestMoveCaptureCount = position.ExpectedMinBestMoveCaptureCount,
                    BestMoveMatchesExpectation = score.Details.Any(detail => detail == "best=ok"),
                    EvaluationSignMatchesExpectation = score.Details.Any(detail => detail == "sign=ok"),
                    LegalMoveCountMatchesExpectation = score.Details.Any(detail => detail == "legal=ok"),
                    CaptureMoveCountMatchesExpectation = score.Details.Any(detail => detail == "captures=ok"),
                    BestMoveCaptureCountMatchesExpectation = score.Details.Any(detail => detail == "bestCaps=ok"),
                    ScoreDetails = score.DetailsText,
                    TranspositionHits = searchResult.TranspositionHits,
                    TranspositionCutoffs = searchResult.TranspositionCutoffs,
                    KillerMoveCutoffs = searchResult.KillerMoveCutoffs,
                    HistoryHeuristicUpdates = searchResult.HistoryHeuristicUpdates
                });
            }
        }

        return new TacticalBenchmarkSummary { Results = results };
    }

    public static void SaveCsv(TacticalBenchmarkSummary summary, string filePath)
    {
        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("profile,evaluator_type,position,side,score,max_score,score_percent,expected_best_move,best_move,best_ok,evaluation,expected_sign,sign_ok,depth,nodes,qnodes,nps,elapsed_ms,legal,expected_legal,legal_ok,captures,expected_captures,captures_ok,best_capture_count_expected,best_capture_ok,pv,top_moves,score_details,stop_reason,tt_hits,tt_cutoffs,killer_cutoffs,history_updates");

        foreach (TacticalBenchmarkPositionResult result in summary.Results)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(result.ProfileName),
                Csv(result.EvaluatorType),
                Csv(result.PositionFile),
                result.SideToMove.ToString(CultureInfo.InvariantCulture),
                result.Score.ToString(CultureInfo.InvariantCulture),
                result.MaximumScore.ToString(CultureInfo.InvariantCulture),
                result.ScorePercent.ToString("0.0", CultureInfo.InvariantCulture),
                Csv(result.ExpectedBestMove),
                Csv(result.BestMove),
                result.BestMoveMatchesExpectation.ToString(CultureInfo.InvariantCulture),
                result.Evaluation.ToString(CultureInfo.InvariantCulture),
                Csv(result.ExpectedEvaluationSign),
                result.EvaluationSignMatchesExpectation.ToString(CultureInfo.InvariantCulture),
                result.CompletedDepth.ToString(CultureInfo.InvariantCulture),
                result.Nodes.ToString(CultureInfo.InvariantCulture),
                result.QuiescenceNodes.ToString(CultureInfo.InvariantCulture),
                result.NodesPerSecond.ToString("0.0", CultureInfo.InvariantCulture),
                result.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture),
                result.LegalMoveCount.ToString(CultureInfo.InvariantCulture),
                result.ExpectedLegalMoveCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result.LegalMoveCountMatchesExpectation.ToString(CultureInfo.InvariantCulture),
                result.CaptureMoveCount.ToString(CultureInfo.InvariantCulture),
                result.ExpectedCaptureMoveCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result.CaptureMoveCountMatchesExpectation.ToString(CultureInfo.InvariantCulture),
                result.ExpectedMinBestMoveCaptureCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result.BestMoveCaptureCountMatchesExpectation.ToString(CultureInfo.InvariantCulture),
                Csv(result.PrincipalVariation),
                Csv(result.TopMoves),
                Csv(result.ScoreDetails),
                Csv(result.StopReason),
                result.TranspositionHits.ToString(CultureInfo.InvariantCulture),
                result.TranspositionCutoffs.ToString(CultureInfo.InvariantCulture),
                result.KillerMoveCutoffs.ToString(CultureInfo.InvariantCulture),
                result.HistoryHeuristicUpdates.ToString(CultureInfo.InvariantCulture)
            }));
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    private static string FormatExpectedBestMoves(TestPosition position)
    {
        List<string> moves = new List<string>();
        if (!string.IsNullOrWhiteSpace(position.ExpectedBestMove))
        {
            moves.Add(position.ExpectedBestMove.Trim());
        }

        if (position.ExpectedBestMoves != null)
        {
            moves.AddRange(position.ExpectedBestMoves.Where(move => !string.IsNullOrWhiteSpace(move)).Select(move => move.Trim()));
        }

        return string.Join(" / ", moves.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value;
    }
}
