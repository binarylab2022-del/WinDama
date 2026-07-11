using System;
using System.Windows.Controls;
using WinDama.Core;

namespace WinDama
{
    /// <summary>
    /// Owns writing SearchResult/SearchProgress values to the right-side analysis panel.
    /// </summary>
    public sealed class AnalysisPanelUpdater
    {
        private readonly TextBlock modeText;
        private readonly TextBlock depthText;
        private readonly TextBlock nodesText;
        private readonly TextBlock evalText;
        private readonly TextBlock bestMoveText;
        private readonly TextBlock timeText;
        private readonly TextBlock nodesPerSecondText;
        private readonly TextBlock legalMovesText;
        private readonly TextBlock budgetText;
        private readonly TextBlock statusText;
        private readonly TextBlock transpositionText;
        private readonly TextBlock principalVariationText;
        private readonly TextBlock topMovesText;

        public AnalysisPanelUpdater(
            TextBlock modeText,
            TextBlock depthText,
            TextBlock nodesText,
            TextBlock evalText,
            TextBlock bestMoveText,
            TextBlock timeText,
            TextBlock nodesPerSecondText,
            TextBlock legalMovesText,
            TextBlock budgetText,
            TextBlock statusText,
            TextBlock transpositionText,
            TextBlock principalVariationText,
            TextBlock topMovesText)
        {
            this.modeText = modeText;
            this.depthText = depthText;
            this.nodesText = nodesText;
            this.evalText = evalText;
            this.bestMoveText = bestMoveText;
            this.timeText = timeText;
            this.nodesPerSecondText = nodesPerSecondText;
            this.legalMovesText = legalMovesText;
            this.budgetText = budgetText;
            this.statusText = statusText;
            this.transpositionText = transpositionText;
            this.principalVariationText = principalVariationText;
            this.topMovesText = topMovesText;
        }

        public void Clear(string currentModeLabel)
        {
            Set(modeText, currentModeLabel);
            Set(depthText, "-");
            Set(nodesText, "-");
            Set(evalText, "-");
            Set(bestMoveText, "-");
            Set(timeText, "-");
            Set(nodesPerSecondText, "-");
            Set(legalMovesText, "-");
            Set(budgetText, "-");
            Set(statusText, "-");
            Set(transpositionText, "-");
            Set(principalVariationText, "-");
            Set(topMovesText, "-");
        }

        public string UpdateFromResult(SearchResult result, string resultModeLabel)
        {
            if (result == null)
            {
                Clear(resultModeLabel);
                return string.Empty;
            }

            Set(modeText, resultModeLabel);
            Set(depthText, GetDepthText(result));
            Set(nodesText, result.QuiescenceNodes > 0
                ? $"{result.Nodes:N0} (q {result.QuiescenceNodes:N0})"
                : result.Nodes.ToString("N0"));
            Set(evalText, result.BestEvaluation.ToString());
            Set(bestMoveText, result.BestMove == null ? "-" : AnalysisTextFormatter.FormatMove(result.BestMove));
            Set(timeText, $"{result.Elapsed.TotalMilliseconds:0} ms");
            Set(nodesPerSecondText, AnalysisTextFormatter.FormatNodesPerSecond(result.NodesPerSecond));
            Set(legalMovesText, $"{result.LegalMoveCount} / {result.CaptureMoveCount}");
            Set(budgetText, result.IsForcedCaptureFastPath ? "skipped" : result.TimeBudgetMilliseconds == null ? "-" : $"{result.TimeBudgetMilliseconds.Value} ms");
            Set(statusText, result.StopReason);
            Set(transpositionText, AnalysisTextFormatter.FormatTranspositionStats(result.TranspositionHits, result.TranspositionCutoffs, result.TranspositionStores, result.TranspositionEntries, result.KillerMoveCutoffs, result.HistoryHeuristicUpdates));
            Set(topMovesText, AnalysisTextFormatter.FormatTopMoves(result.TopMoves));
            Set(principalVariationText, AnalysisTextFormatter.FormatPrincipalVariation(result.PrincipalVariation));

            if (result.IsForcedCaptureFastPath)
            {
                return $"Mandatory capture: {result.StopReason}. Selected {AnalysisTextFormatter.FormatMove(result.BestMove)}. PV: {AnalysisTextFormatter.FormatPrincipalVariationInline(result.PrincipalVariation)}";
            }

            string timeoutText = result.TimedOut ? " Time limit reached." : string.Empty;
            string qText = result.QuiescenceNodes > 0 ? $", q-nodes {result.QuiescenceNodes:N0}" : string.Empty;
            return $"Depth {result.CompletedDepth}, eval {result.BestEvaluation}, nodes {result.Nodes:N0}{qText}, N/s {AnalysisTextFormatter.FormatNodesPerSecond(result.NodesPerSecond)}, legal/cap {result.LegalMoveCount}/{result.CaptureMoveCount}, TT hits/cut {result.TranspositionHits:N0}/{result.TranspositionCutoffs:N0}, killer/hist {result.KillerMoveCutoffs:N0}/{result.HistoryHeuristicUpdates:N0}, near-best {result.NearBestMoveCount}.{timeoutText}";
        }

        public string UpdateFromProgress(SearchProgress progress, string currentModeLabel)
        {
            if (progress == null)
            {
                Clear(currentModeLabel);
                return string.Empty;
            }

            Set(modeText, currentModeLabel);
            Set(depthText, progress.IsForcedCaptureFastPath
                ? progress.CompletedDepth > 0 ? $"forced d{progress.CompletedDepth}" : "forced"
                : $"{progress.CurrentDepth} / {progress.CompletedDepth}");
            Set(nodesText, progress.QuiescenceNodes > 0
                ? $"{progress.Nodes:N0} (q {progress.QuiescenceNodes:N0})"
                : progress.Nodes.ToString("N0"));
            Set(evalText, progress.BestEvaluation.ToString());
            Set(bestMoveText, progress.BestMove == null ? "-" : AnalysisTextFormatter.FormatMove(progress.BestMove));
            Set(timeText, $"{progress.Elapsed.TotalMilliseconds:0} ms");
            Set(nodesPerSecondText, AnalysisTextFormatter.FormatNodesPerSecond(progress.NodesPerSecond));
            Set(legalMovesText, $"{progress.LegalMoveCount} / {progress.CaptureMoveCount}");

            if (progress.IsForcedCaptureFastPath)
            {
                Set(budgetText, "skipped");
            }

            string currentMoveText = progress.CurrentMove == null ? string.Empty : $" | current {AnalysisTextFormatter.FormatMove(progress.CurrentMove)}";
            Set(statusText, $"{progress.Message}{currentMoveText}");
            Set(transpositionText, AnalysisTextFormatter.FormatTranspositionStats(progress.TranspositionHits, progress.TranspositionCutoffs, progress.TranspositionStores, progress.TranspositionEntries, progress.KillerMoveCutoffs, progress.HistoryHeuristicUpdates));
            Set(topMovesText, AnalysisTextFormatter.FormatTopMoves(progress.TopMoves));
            Set(principalVariationText, AnalysisTextFormatter.FormatPrincipalVariation(progress.PrincipalVariation));

            if (progress.IsForcedCaptureFastPath)
            {
                return $"Mandatory capture: {progress.Message}. PV: {AnalysisTextFormatter.FormatPrincipalVariationInline(progress.PrincipalVariation)}";
            }

            string qText = progress.QuiescenceNodes > 0 ? $", q-nodes {progress.QuiescenceNodes:N0}" : string.Empty;
            return $"Searching depth {progress.CurrentDepth}, completed {progress.CompletedDepth}, nodes {progress.Nodes:N0}{qText}, eval {progress.BestEvaluation}, N/s {AnalysisTextFormatter.FormatNodesPerSecond(progress.NodesPerSecond)}, TT hits/cut {progress.TranspositionHits:N0}/{progress.TranspositionCutoffs:N0}, killer/hist {progress.KillerMoveCutoffs:N0}/{progress.HistoryHeuristicUpdates:N0}.";
        }

        private static string GetDepthText(SearchResult result)
        {
            if (result.IsForcedCaptureFastPath)
            {
                return result.CompletedDepth > 0 ? $"forced d{result.CompletedDepth}" : "forced";
            }

            return result.CompletedDepth == 0 ? "0" : result.CompletedDepth.ToString();
        }

        private static void Set(TextBlock target, string value)
        {
            if (target != null)
            {
                target.Text = value;
            }
        }
    }
}
