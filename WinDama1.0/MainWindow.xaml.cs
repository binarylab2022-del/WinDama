using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Media;
using System.Windows.Threading;
using System.Threading;
using System.IO;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.Win32;

using WinDama.Core;
namespace WinDama
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int[,] board = new int[8, 8]
{
    { -1, 0, -1, 0, -1, 0, -1, 0 },
     { 0, -1, 0, -1, 0, -1, 0, -1 },
    { -1, 0, -1, 0, -1, 0, -1, 0 },
    { 0, 0, 0, 0, 0, 0, 0, 0 },
    { 0, 0, 0, 0, 0, 0, 0, 0 },
    { 0, 1, 0, 1, 0, 1, 0, 1 },
    { 1, 0, 1, 0, 1, 0, 1, 0 },
    { 0, 1, 0, 1, 0, 1, 0, 1 },
};
        private int currentPlayer = 1;
        private bool pieceSelected = false;
        private Stack<UiGameSnapshot> RedoStack = new Stack<UiGameSnapshot>();
        private int? selectedColumn = null;
        private int? selectedRow = null;
        private readonly List<Move> highlightedLegalMoves = new List<Move>();
        private Move? lastMove;
        private const int AiTieBreakTolerance = 0;
        private const int BoardSquareSize = 80;
        private const int BoardOffset = 30;
        private const string ApplicationVersion = "1.0.0-preview";
        private const string EngineVersion = "Core engine 2026.07";
        private const string TestBaseline = "101 passing tests baseline";
        private readonly BoardRenderer boardRenderer = new BoardRenderer(BoardSquareSize, BoardOffset);
        private BoardEditorController? boardEditorController;
        private AnalysisPanelUpdater? analysisPanelUpdater;
        private ClockController? clockController;
        private int MinimaxDepth = 5;
        private AiSearchMode selectedAiSearchMode = AiSearchMode.FixedDepth;
        private GameMode selectedGameMode = GameMode.HumanVsAI;
        private bool isAiMoveInProgress = false;
        private long gameStateVersion = 0;
        private const int MaxIterativeSearchDepth = 64;
        private const int DefaultAiMoveTimeMilliseconds = 3000;
        private const int MinAiMoveTimeMilliseconds = 200;
        private const int MaxAiMoveTimeMilliseconds = 30000;
        private const int GameClockSafetyMilliseconds = 2000;
        private const int GameClockMovesToGoEstimate = 25;
        private const int ForcedCaptureDisplayDelayMilliseconds = 650;
        private readonly SoundPlayer moveSoundPlayer;
        // Player 1 starts
        //private Stack<Move> moveHistory = new Stack<Move>();
        //private Stack<Move> redoStack = new Stack<Move>();
        private Stack<UiGameSnapshot> UndoStack = new Stack<UiGameSnapshot>();
        private readonly MoveGenerator moveGenerator = new MoveGenerator();
        private readonly SearchEngine searchEngine = new SearchEngine();
        private readonly Evaluation positionEvaluator = new Evaluation();
        private readonly GameController gameController;
        private bool isGameOver = false;
        private bool isRestoringSnapshot = false;
        private CancellationTokenSource? backgroundAnalysisCancellation;
        private long backgroundAnalysisRequestId = 0;
        // Background analysis is intentionally unbounded: it keeps deepening
        // until the position changes, the user starts another analysis, or the
        // window/game state cancels the current request.
        private const int BackgroundAnalysisInfiniteDepth = MaxIterativeSearchDepth;
        private bool isBoardEditorMode = false;
        private int editorSelectedPieceValue = 1;
        private static readonly string DefaultTestPositionsFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WinDama Test Positions");
        private const string BundledOpeningBooksFolderName = "OpeningBooks";
        private TacticalBenchmarkSummary? latestBenchmarkSummary;
        private bool isBenchmarkRunning = false;
        private EvaluationWeightTuningSummary? latestTuningSummary;
        private bool isTuningRunning = false;
        private EvaluationTournamentSummary? latestTournamentSummary;
        private bool isTournamentRunning = false;
        private GameDatabase? latestGameDatabase;
        private OpeningBook? openingBook;
        private PositionDataset? latestPositionDataset;
        private bool isDatasetExportRunning = false;
        private LinearEvaluationTrainingResult? latestLinearTrainingResult;
        private LinearEvaluationModel? latestLinearModel;
        private bool isLinearTrainingRunning = false;
        private bool isContinuousAnalysisEnabled = true;
        private bool isBackgroundAnalysisPaused = false;
        private bool isApplyingUserSettings = false;
        private static readonly string UserSettingsFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinDama");
        private static readonly string UserSettingsFile = System.IO.Path.Combine(UserSettingsFolder, "settings.json");

        private enum AiSearchMode
        {
            FixedDepth,
            FixedTimePerMove,
            GameClock
        }

        private sealed class SearchConfiguration
        {
            public AiSearchMode Mode { get; set; }
            public int MaximumDepth { get; set; }
            public int? TimeLimitMilliseconds { get; set; }
            public bool Iterative { get; set; }
            public int ForcedCaptureComparisonDepth { get; set; }
            public bool UseLinearEvaluator { get; set; }
            public LinearEvaluationModel? LinearModel { get; set; }
        }

        private sealed class AppUserSettings
        {
            public int GameModeIndex { get; set; } = 0;
            public int AiSearchModeIndex { get; set; } = 0;
            public int MinimaxDepth { get; set; } = 5;
            public string AiTimePerMoveMilliseconds { get; set; } = "3000";
            public string GameClockMinutes { get; set; } = "5";
            public bool AutoAnalyzeEnabled { get; set; } = true;
            public bool AnalysisPaused { get; set; } = false;
            public bool UseOpeningBook { get; set; } = true;
            public string OpeningBookMaxPlies { get; set; } = "16";
            public string OpeningBookMinGames { get; set; } = "1";
            public int OpeningBookModeIndex { get; set; } = 0;
            public bool UseLinearEvaluator { get; set; } = false;
        }

        private sealed class UiGameSnapshot
        {
            public WinDama.Core.GameSnapshot? CoreSnapshot { get; set; }
            public int[,] Board { get; set; } = new int[8, 8]; 
            public int CurrentPlayer { get; set; }
            public Move? LastMove { get; set; }
            public TimeSpan Player1TimeRemaining { get; set; }
            public TimeSpan Player2TimeRemaining { get; set; }
            public bool IsGameOver { get; set; }
            public AiSearchMode SelectedAiSearchMode { get; set; }
            public GameMode SelectedGameMode { get; set; }
            public int MinimaxDepth { get; set; }
            public string AiTimePerMoveText { get; set; } = string.Empty;
            public string GameClockMinutesText { get; set; } = string.Empty;
            public string StatusText { get; set; } = string.Empty;
            public string AnalysisSummaryText { get; set; } = string.Empty;
            public string SearchModeText { get; set; } = string.Empty;
            public string SearchDepthText { get; set; } = string.Empty;
            public string SearchNodesText { get; set; } = string.Empty;
            public string SearchEvalText { get; set; } = string.Empty;
            public string SearchBestMoveText { get; set; } = string.Empty;
            public string SearchTimeText { get; set; } = string.Empty;
            public string SearchNodesPerSecondText { get; set; } = string.Empty;
            public string SearchLegalMovesText { get; set; } = string.Empty;
            public string SearchBudgetText { get; set; } = string.Empty;
            public string SearchStatusText { get; set; } = string.Empty;
            public string SearchTranspositionText { get; set; } = string.Empty;
            public string SearchTopMovesText { get; set; } = string.Empty;
            public string SearchPrincipalVariationText { get; set; } = string.Empty;
            public bool IsBoardEditorMode { get; set; }
            public int EditorSelectedPieceValue { get; set; }
            public string PositionNameText { get; set; } = string.Empty;
            public string PositionNotesText { get; set; } = string.Empty;
        }

        //-----------------------------------------------------------------------
        public MainWindow()
        {
            // Create the core controller before InitializeComponent().
            // WPF can raise SelectionChanged events while XAML is being loaded,
            // so event handlers must not see a null controller.
            gameController = new GameController(board, currentPlayer, selectedGameMode, moveGenerator);

            InitializeComponent();
            InitializeUiHelpers();
            LoadTournamentProfileSelectors();
            if (!TryLoadBundledOpeningBook(showMessageOnError: false))
            {
                UpdateOpeningBookSummary("No built-in opening book found. Import .ouv, load a database, or run a tournament.");
            }
            LoadApplicationSettings();
            UpdateDatasetSummary("No dataset built. Run/load a game database first.");
            UpdateAnalysisControlButtons();
            UpdateEngineStatusBar();
            SynchronizeFieldsFromGameController();
            InitializeChessClock();



            moveSoundPlayer = new SoundPlayer(WinDama.Properties.Resources.Ding);

            // Optionally, you can load the sound into memory for quicker playback
            moveSoundPlayer.Load();
        }

        private void InitializeUiHelpers()
        {
            boardEditorController = new BoardEditorController(EditorPieceComboBox, EditorSideToMoveComboBox, EditorHintTextBlock);
            analysisPanelUpdater = new AnalysisPanelUpdater(
                SearchModeValueTextBlock,
                SearchDepthTextBlock,
                SearchNodesTextBlock,
                SearchEvalTextBlock,
                SearchBestMoveTextBlock,
                SearchTimeTextBlock,
                SearchNodesPerSecondTextBlock,
                SearchLegalMovesTextBlock,
                SearchBudgetTextBlock,
                SearchStatusTextBlock,
                SearchTranspositionTextBlock,
                SearchPrincipalVariationTextBlock,
                SearchTopMovesTextBlock);
        }

        //-----------------------------------------------------------------------
        private void InitializeChessClock()
        {
            clockController = new ClockController(player1ClockLabel, player2ClockLabel);
            clockController.PlayerTimedOut += OnPlayerTimedOut;
            clockController.Reset(TimeSpan.FromMinutes(5));
        }

        private void LoadApplicationSettings()
        {
            isApplyingUserSettings = true;
            bool previousRestoring = isRestoringSnapshot;
            isRestoringSnapshot = true;
            try
            {
                AppUserSettings settings = ReadApplicationSettings();
                ApplyApplicationSettings(settings);
            }
            finally
            {
                isRestoringSnapshot = previousRestoring;
                isApplyingUserSettings = false;
            }
        }

        private static AppUserSettings ReadApplicationSettings()
        {
            try
            {
                if (!System.IO.File.Exists(UserSettingsFile))
                {
                    return new AppUserSettings();
                }

                string json = System.IO.File.ReadAllText(UserSettingsFile, Encoding.UTF8);
                return JsonSerializer.Deserialize<AppUserSettings>(json) ?? new AppUserSettings();
            }
            catch
            {
                return new AppUserSettings();
            }
        }

        private void ApplyApplicationSettings(AppUserSettings settings)
        {
            int gameModeIndex = Clamp(settings.GameModeIndex, 0, 2);
            if (GameModeComboBox != null)
            {
                GameModeComboBox.SelectedIndex = gameModeIndex;
            }
            selectedGameMode = gameModeIndex switch
            {
                1 => GameMode.HumanVsHuman,
                2 => GameMode.AIVsAI,
                _ => GameMode.HumanVsAI
            };
            gameController.SetGameMode(selectedGameMode);

            if (AiSearchModeComboBox != null)
            {
                AiSearchModeComboBox.SelectedIndex = Clamp(settings.AiSearchModeIndex, 0, 2);
                selectedAiSearchMode = AiSearchModeComboBox.SelectedIndex switch
                {
                    1 => AiSearchMode.FixedTimePerMove,
                    2 => AiSearchMode.GameClock,
                    _ => AiSearchMode.FixedDepth
                };
            }

            MinimaxDepth = Clamp(settings.MinimaxDepth, 1, 18);
            if (MinimaxDepthTextBox != null)
            {
                MinimaxDepthTextBox.Text = MinimaxDepth.ToString();
            }

            if (AiTimePerMoveTextBox != null)
            {
                AiTimePerMoveTextBox.Text = settings.AiTimePerMoveMilliseconds;
            }

            if (GameClockMinutesTextBox != null)
            {
                GameClockMinutesTextBox.Text = settings.GameClockMinutes;
            }

            isContinuousAnalysisEnabled = settings.AutoAnalyzeEnabled;
            isBackgroundAnalysisPaused = settings.AnalysisPaused;
            if (AutoAnalyzeCheckBox != null)
            {
                AutoAnalyzeCheckBox.IsChecked = isContinuousAnalysisEnabled;
            }

            if (UseOpeningBookCheckBox != null)
            {
                UseOpeningBookCheckBox.IsChecked = settings.UseOpeningBook;
            }

            if (OpeningBookMaxPliesTextBox != null)
            {
                OpeningBookMaxPliesTextBox.Text = settings.OpeningBookMaxPlies;
            }

            if (OpeningBookMinGamesTextBox != null)
            {
                OpeningBookMinGamesTextBox.Text = settings.OpeningBookMinGames;
            }

            if (OpeningBookModeComboBox != null)
            {
                OpeningBookModeComboBox.SelectedIndex = Clamp(settings.OpeningBookModeIndex, 0, 2);
            }

            if (UseLinearEvaluatorCheckBox != null)
            {
                UseLinearEvaluatorCheckBox.IsChecked = settings.UseLinearEvaluator;
            }

            RefreshSearchModeUi();
        }

        private AppUserSettings CaptureApplicationSettings()
        {
            return new AppUserSettings
            {
                GameModeIndex = GameModeComboBox?.SelectedIndex ?? 0,
                AiSearchModeIndex = AiSearchModeComboBox?.SelectedIndex ?? 0,
                MinimaxDepth = MinimaxDepth,
                AiTimePerMoveMilliseconds = AiTimePerMoveTextBox?.Text ?? "3000",
                GameClockMinutes = GameClockMinutesTextBox?.Text ?? "5",
                AutoAnalyzeEnabled = isContinuousAnalysisEnabled,
                AnalysisPaused = isBackgroundAnalysisPaused,
                UseOpeningBook = UseOpeningBookCheckBox?.IsChecked == true,
                OpeningBookMaxPlies = OpeningBookMaxPliesTextBox?.Text ?? "16",
                OpeningBookMinGames = OpeningBookMinGamesTextBox?.Text ?? "1",
                OpeningBookModeIndex = OpeningBookModeComboBox?.SelectedIndex ?? 0,
                UseLinearEvaluator = UseLinearEvaluatorCheckBox?.IsChecked == true
            };
        }

        private void SaveApplicationSettingsSafe()
        {
            if (isApplyingUserSettings)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(UserSettingsFolder);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CaptureApplicationSettings(), options);
                System.IO.File.WriteAllText(UserSettingsFile, json, Encoding.UTF8);
            }
            catch
            {
                // Settings persistence must never break gameplay.
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveApplicationSettingsSafe();
            CancelBackgroundPositionAnalysis();
            base.OnClosing(e);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        //-----------------------------------------------------------------------
        private void OnPlayerTimedOut(int player)
        {
            string message = player == 1
                ? "Player 1 ran out of time. Player 2 wins on time."
                : "Player 2 ran out of time. Player 1 wins on time.";

            gameController.RestoreState(board, currentPlayer, isGameOver: true, lastMove: lastMove);
            SynchronizeFieldsFromGameController();
            StopAllTimers();
            SetStatus(message);
            SetAnalysisSummary("Game ended by clock.");
        }

        //-----------------------------------------------------------------------
        private void SynchronizeFieldsFromGameController()
        {
            board = gameController.Board;
            currentPlayer = gameController.CurrentPlayer;
            isGameOver = gameController.IsGameOver;
            selectedGameMode = gameController.GameMode;
            lastMove = CloneMove(gameController.LastMove);
        }

        private void StopAllTimers()
        {
            clockController?.StopAll();
        }

        private void StartTimerForCurrentPlayer()
        {
            clockController?.StartForPlayer(currentPlayer);
        }

        //-----------------------------------------------------------------------
        private bool TryGetBoardCellFromPoint(Point point, out int row, out int column)
        {
            return boardRenderer.TryGetBoardCellFromPoint(myCanvas, point, out row, out column);
        }

        private void BoardCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (myCanvas == null || board == null)
            {
                return;
            }

            DrawBoard(myCanvas, board);
        }

        //-----------------------------------------------------------------------
        public void DrawBoard(Canvas canvas, int[,] board)
        {
            boardRenderer.Draw(canvas, board, pieceSelected, selectedRow, selectedColumn, highlightedLegalMoves, lastMove);
        }

        //-----------------------------------------------------------------------
        // Legacy flip-board debug helper removed.

        //-----------------------------------------------------------------------
        private void IncreaseDepth_Click(object sender, RoutedEventArgs e)
        {
            int currentDepth = GetConfiguredDepth();
            int newDepth = Math.Min(18, currentDepth + 1);
            MinimaxDepthTextBox.Text = newDepth.ToString();
            MinimaxDepth = newDepth;
            UpdateAnalysisFields(null);
            StartBackgroundPositionAnalysis("Depth changed");
        }
        //-----------------------------------------------------------------------
        private void DecreaseDepth_Click(object sender, RoutedEventArgs e)
        {
            int currentDepth = GetConfiguredDepth();
            int newDepth = Math.Max(1, currentDepth - 1);
            MinimaxDepthTextBox.Text = newDepth.ToString();
            MinimaxDepth = newDepth;
            UpdateAnalysisFields(null);
            StartBackgroundPositionAnalysis("Depth changed");
        }

        //-----------------------------------------------------------------------
        private void UpdateMinimaxDepth(int newDepth)
        {
            // Update the Minimax depth in your application
            MinimaxDepth = newDepth;
            // Apply any necessary logic based on the updated depth
        }

        //-----------------------------------------------------------------------
        private void ChessBoardGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Use the actual rendered canvas coordinates. This keeps clicking accurate
            // when the window is maximized or the board is resized.
            Point clickedPoint = e.GetPosition(myCanvas);

            if (!TryGetBoardCellFromPoint(clickedPoint, out int row, out int column))
            {
                ClearMoveSelection();
                e.Handled = true;
                return;
            }

            HandleSquareClick(row, column);
            e.Handled = true;
        }

        //-----------------------------------------------------------------------        
        // Legacy/debug square-click and local game-over helpers were removed.
        // Selection, legal-move display, game-over, and search now flow through
        // HandleSquareClick, MoveGenerator, GameController, and SearchEngine.

        //-----------------------------------------------------------------------
        private void SetStatus(string message)
        {
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = message;
            }

            UpdateEngineStatusBar();
        }

        //-----------------------------------------------------------------------
        private void SetAnalysisSummary(string message)
        {
            if (AnalysisSummaryTextBlock != null)
            {
                AnalysisSummaryTextBlock.Text = message;
            }

            UpdateEngineStatusBar();
        }

        //-----------------------------------------------------------------------
        private void UpdateEngineStatusBar()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateEngineStatusBar));
                return;
            }

            if (EngineStatusTextBlock == null || VersionStatusTextBlock == null)
            {
                return;
            }

            string searchMode = selectedAiSearchMode switch
            {
                AiSearchMode.FixedTimePerMove => $"fixed time {AiTimePerMoveTextBox?.Text ?? "3000"} ms",
                AiSearchMode.GameClock => $"game clock {GameClockMinutesTextBox?.Text ?? "5"} min",
                _ => $"depth {MinimaxDepth}"
            };

            string evaluator = UseLinearEvaluatorCheckBox?.IsChecked == true && latestLinearModel != null
                ? $"learned linear ({latestLinearModel.Name})"
                : "handcrafted";

            string book = UseOpeningBookCheckBox?.IsChecked == true && openingBook != null
                ? $"book on: {openingBook.EntryCount:N0} entries"
                : "book off";

            string analysis = !isContinuousAnalysisEnabled
                ? "analysis stopped"
                : isBackgroundAnalysisPaused
                    ? "analysis paused"
                    : "analysis auto";

            EngineStatusTextBlock.Text =
                $"{GetGameModeLabel(selectedGameMode)} | {searchMode} | eval: {evaluator} | {book} | TT+QS+K/H on | {analysis}";

            VersionStatusTextBlock.Text = $"WinDama {ApplicationVersion} | {EngineVersion} | {TestBaseline}";
        }

        //-----------------------------------------------------------------------
        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateEngineStatusBar();

            string evaluator = UseLinearEvaluatorCheckBox?.IsChecked == true && latestLinearModel != null
                ? $"Learned linear evaluator: {latestLinearModel.Name}"
                : "Handcrafted evaluator profile";

            string bookStatus = openingBook != null
                ? $"Opening book: {openingBook.EntryCount:N0} entries across {openingBook.PositionCount:N0} positions"
                : "Opening book: not loaded";

            string message =
                $"WinDama - Algerian / Spanish Checkers\n" +
                $"Version: {ApplicationVersion}\n" +
                $"Engine: {EngineVersion}\n" +
                $"Validation: {TestBaseline}\n\n" +
                $"Active mode: {GetGameModeLabel(selectedGameMode)}\n" +
                $"AI search: {selectedAiSearchMode}, depth {MinimaxDepth}\n" +
                $"Evaluator: {evaluator}\n" +
                $"{bookStatus}\n" +
                $"Continuous analysis: {(isContinuousAnalysisEnabled ? (isBackgroundAnalysisPaused ? "paused" : "enabled") : "stopped")}\n\n" +
                "Engine features:\n" +
                "- mandatory longest-capture Spanish checkers rules\n" +
                "- alpha-beta search with iterative deepening\n" +
                "- principal variation and top-5 candidate moves\n" +
                "- transposition table, quiescence search, killer/history heuristics\n" +
                "- bundled .ouv opening book with mirrored orientation support\n" +
                "- tactical benchmark, evaluator tuner, tournaments, datasets, learned linear evaluator\n\n" +
                "Settings are stored in %APPDATA%\\WinDama\\settings.json.";

            MessageBox.Show(message, "About WinDama", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //-----------------------------------------------------------------------
        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGame();
        }

        //-----------------------------------------------------------------------
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (UndoStack.Count == 0)
            {
                SetStatus("Nothing to undo.");
                return;
            }

            UiGameSnapshot current = CreateSnapshot();
            UiGameSnapshot previous = UndoStack.Pop();
            RedoStack.Push(current);
            RestoreSnapshot(previous, "Undo applied");
        }

        //-----------------------------------------------------------------------
        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (RedoStack.Count == 0)
            {
                SetStatus("Nothing to redo.");
                return;
            }

            UiGameSnapshot current = CreateSnapshot();
            UiGameSnapshot next = RedoStack.Pop();
            UndoStack.Push(current);
            RestoreSnapshot(next, "Redo applied");
        }

        //-----------------------------------------------------------------------
        private UiGameSnapshot CreateSnapshot()
        {
            return new UiGameSnapshot
            {
                CoreSnapshot = gameController.CreateSnapshot(),
                Board = (int[,])board.Clone(),
                CurrentPlayer = currentPlayer,
                LastMove = CloneMove(lastMove),
                Player1TimeRemaining = clockController?.Player1TimeRemaining ?? TimeSpan.Zero,
                Player2TimeRemaining = clockController?.Player2TimeRemaining ?? TimeSpan.Zero,
                IsGameOver = isGameOver,
                SelectedAiSearchMode = selectedAiSearchMode,
                SelectedGameMode = selectedGameMode,
                MinimaxDepth = GetConfiguredDepth(),
                AiTimePerMoveText = AiTimePerMoveTextBox?.Text ?? string.Empty,
                GameClockMinutesText = GameClockMinutesTextBox?.Text ?? string.Empty,
                StatusText = StatusTextBlock?.Text ?? string.Empty,
                AnalysisSummaryText = AnalysisSummaryTextBlock?.Text ?? string.Empty,
                SearchModeText = SearchModeValueTextBlock?.Text ?? string.Empty,
                SearchDepthText = SearchDepthTextBlock?.Text ?? string.Empty,
                SearchNodesText = SearchNodesTextBlock?.Text ?? string.Empty,
                SearchEvalText = SearchEvalTextBlock?.Text ?? string.Empty,
                SearchBestMoveText = SearchBestMoveTextBlock?.Text ?? string.Empty,
                SearchTimeText = SearchTimeTextBlock?.Text ?? string.Empty,
                SearchNodesPerSecondText = SearchNodesPerSecondTextBlock?.Text ?? string.Empty,
                SearchLegalMovesText = SearchLegalMovesTextBlock?.Text ?? string.Empty,
                SearchBudgetText = SearchBudgetTextBlock?.Text ?? string.Empty,
                SearchStatusText = SearchStatusTextBlock?.Text ?? string.Empty,
                SearchTranspositionText = SearchTranspositionTextBlock?.Text ?? string.Empty,
                SearchTopMovesText = SearchTopMovesTextBlock?.Text ?? string.Empty,
                SearchPrincipalVariationText = SearchPrincipalVariationTextBlock?.Text ?? string.Empty,
                IsBoardEditorMode = isBoardEditorMode,
                EditorSelectedPieceValue = editorSelectedPieceValue,
                PositionNameText = PositionNameTextBox?.Text ?? string.Empty,
                PositionNotesText = PositionNotesTextBox?.Text ?? string.Empty
            };
        }

        //-----------------------------------------------------------------------
        private void RestoreSnapshot(UiGameSnapshot snapshot, string reason)
        {
            if (snapshot == null)
            {
                return;
            }

            gameStateVersion++;
            CancelBackgroundPositionAnalysis();
            StopAllTimers();
            ClearMoveSelection(redraw: false);

            isRestoringSnapshot = true;
            try
            {
                selectedGameMode = snapshot.SelectedGameMode;
                selectedAiSearchMode = snapshot.SelectedAiSearchMode;
                MinimaxDepth = Math.Max(1, snapshot.MinimaxDepth);
                isBoardEditorMode = snapshot.IsBoardEditorMode;
                editorSelectedPieceValue = snapshot.EditorSelectedPieceValue;

                gameController.SetGameMode(selectedGameMode);
                if (snapshot.CoreSnapshot != null)
                {
                    gameController.RestoreSnapshot(snapshot.CoreSnapshot);
                }
                else
                {
                    gameController.RestoreState(snapshot.Board, snapshot.CurrentPlayer, snapshot.IsGameOver, snapshot.LastMove);
                }
                gameController.SetGameMode(selectedGameMode);
                SynchronizeFieldsFromGameController();

                if (snapshot.IsGameOver)
                {
                    isGameOver = true;
                }

                SetGameModeComboBoxFromMode(selectedGameMode);
                if (AiSearchModeComboBox != null)
                {
                    AiSearchModeComboBox.SelectedIndex = selectedAiSearchMode switch
                    {
                        AiSearchMode.FixedTimePerMove => 1,
                        AiSearchMode.GameClock => 2,
                        _ => 0
                    };
                }

                if (MinimaxDepthTextBox != null)
                {
                    MinimaxDepthTextBox.Text = MinimaxDepth.ToString();
                }
                if (AiTimePerMoveTextBox != null && !string.IsNullOrWhiteSpace(snapshot.AiTimePerMoveText))
                {
                    AiTimePerMoveTextBox.Text = snapshot.AiTimePerMoveText;
                }
                if (GameClockMinutesTextBox != null && !string.IsNullOrWhiteSpace(snapshot.GameClockMinutesText))
                {
                    GameClockMinutesTextBox.Text = snapshot.GameClockMinutesText;
                }

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = snapshot.StatusText;
                }
                if (AnalysisSummaryTextBlock != null)
                {
                    AnalysisSummaryTextBlock.Text = snapshot.AnalysisSummaryText;
                }
                if (SearchModeValueTextBlock != null) SearchModeValueTextBlock.Text = snapshot.SearchModeText;
                if (SearchDepthTextBlock != null) SearchDepthTextBlock.Text = snapshot.SearchDepthText;
                if (SearchNodesTextBlock != null) SearchNodesTextBlock.Text = snapshot.SearchNodesText;
                if (SearchEvalTextBlock != null) SearchEvalTextBlock.Text = snapshot.SearchEvalText;
                if (SearchBestMoveTextBlock != null) SearchBestMoveTextBlock.Text = snapshot.SearchBestMoveText;
                if (SearchTimeTextBlock != null) SearchTimeTextBlock.Text = snapshot.SearchTimeText;
                if (SearchNodesPerSecondTextBlock != null) SearchNodesPerSecondTextBlock.Text = snapshot.SearchNodesPerSecondText;
                if (SearchLegalMovesTextBlock != null) SearchLegalMovesTextBlock.Text = snapshot.SearchLegalMovesText;
                if (SearchBudgetTextBlock != null) SearchBudgetTextBlock.Text = snapshot.SearchBudgetText;
                if (SearchStatusTextBlock != null) SearchStatusTextBlock.Text = snapshot.SearchStatusText;
                if (SearchTranspositionTextBlock != null) SearchTranspositionTextBlock.Text = snapshot.SearchTranspositionText;
                if (SearchTopMovesTextBlock != null) SearchTopMovesTextBlock.Text = snapshot.SearchTopMovesText;
                if (SearchPrincipalVariationTextBlock != null) SearchPrincipalVariationTextBlock.Text = snapshot.SearchPrincipalVariationText;

                if (PositionNameTextBox != null) PositionNameTextBox.Text = snapshot.PositionNameText;
                if (PositionNotesTextBox != null) PositionNotesTextBox.Text = snapshot.PositionNotesText;

                if (clockController != null)
                {
                    clockController.SetTimes(snapshot.Player1TimeRemaining, snapshot.Player2TimeRemaining);
                }

                boardEditorController?.SetSelectedPieceValue(editorSelectedPieceValue);
                SetEditorSideToMove(currentPlayer);
            }
            finally
            {
                isRestoringSnapshot = false;
            }

            RefreshSearchModeUi();
            RefreshGameModeUi();
            RefreshEditorUiFromState();
            UpdateBoardUI();
            UpdateCurrentPlayerLabel();
            gameStateVersion++;

            SetStatus(reason);
            if (!isGameOver && !isBoardEditorMode)
            {
                StartTimerForCurrentPlayer();
                StartBackgroundPositionAnalysis(reason);
            }
        }

        //-----------------------------------------------------------------------
        private void UpdateBoardUI()
        {
            if (myCanvas != null && board != null)
            {
                DrawBoard(myCanvas, board);
            }
        }

        //-----------------------------------------------------------------------
        private void UpdateCurrentPlayerLabel()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateCurrentPlayerLabel));
                return;
            }

            string controllerStatus = gameController?.GetStatusMessage() ?? string.Empty;
            string label = GetPlayerLabel(currentPlayer);
            string controllerText = isGameOver && !string.IsNullOrWhiteSpace(controllerStatus)
                ? controllerStatus
                : $"{label} to move ({(IsAiControlledPlayer(currentPlayer) ? "AI" : "Human")})";

            if (StatusTextBlock != null && string.IsNullOrWhiteSpace(StatusTextBlock.Text))
            {
                StatusTextBlock.Text = controllerText;
            }

            if (GameModeValueTextBlock != null)
            {
                GameModeValueTextBlock.Text = $"{GetGameModeLabel(selectedGameMode)} — {controllerText}";
            }
        }

        //-----------------------------------------------------------------------
        private void GameModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameModeComboBox == null || isRestoringSnapshot || gameController == null)
            {
                return;
            }

            selectedGameMode = GameModeComboBox.SelectedIndex switch
            {
                1 => GameMode.HumanVsHuman,
                2 => GameMode.AIVsAI,
                _ => GameMode.HumanVsAI
            };
            gameController.SetGameMode(selectedGameMode);
            SynchronizeFieldsFromGameController();
            gameStateVersion++;

            RefreshGameModeUi();
            ClearMoveSelection();
            ScheduleAIMoveIfNeeded();
            StartBackgroundPositionAnalysis("Game mode changed");
        }

        //-----------------------------------------------------------------------
        private void RefreshGameModeUi()
        {
            if (GameModeValueTextBlock != null)
            {
                GameModeValueTextBlock.Text = GetGameModeLabel(selectedGameMode);
            }

            if (player1RoleLabel != null)
            {
                player1RoleLabel.Text = IsAiControlledPlayer(1) ? "Player 1 / AI" : "Player 1 / Human";
            }

            if (player2RoleLabel != null)
            {
                player2RoleLabel.Text = IsAiControlledPlayer(-1) ? "Player 2 / AI" : "Player 2 / Human";
            }

            UpdateCurrentPlayerLabel();
        }

        //-----------------------------------------------------------------------
        private string GetGameModeLabel(GameMode mode)
        {
            return mode switch
            {
                GameMode.HumanVsHuman => "Human vs Human",
                GameMode.AIVsAI => "AI vs AI",
                _ => "Human vs AI"
            };
        }

        //-----------------------------------------------------------------------
        private bool IsAiControlledPlayer(int player)
        {
            if (gameController != null)
            {
                return gameController.IsAiControlledPlayer(player);
            }

            return selectedGameMode switch
            {
                GameMode.HumanVsHuman => false,
                GameMode.AIVsAI => true,
                _ => player == -1
            };
        }

        //-----------------------------------------------------------------------
        private bool IsHumanControlledPlayer(int player)
        {
            return !IsAiControlledPlayer(player);
        }

        //-----------------------------------------------------------------------
        private void AiSearchModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AiSearchModeComboBox == null || isRestoringSnapshot)
            {
                return;
            }

            selectedAiSearchMode = AiSearchModeComboBox.SelectedIndex switch
            {
                1 => AiSearchMode.FixedTimePerMove,
                2 => AiSearchMode.GameClock,
                _ => AiSearchMode.FixedDepth
            };

            RefreshSearchModeUi();
            UpdateAnalysisFields(null);
            StartBackgroundPositionAnalysis("Search mode changed");
        }

        //-----------------------------------------------------------------------
        private void RefreshSearchModeUi()
        {
            bool usesDepth = selectedAiSearchMode == AiSearchMode.FixedDepth;
            bool usesMoveTime = selectedAiSearchMode == AiSearchMode.FixedTimePerMove;
            bool usesGameClock = selectedAiSearchMode == AiSearchMode.GameClock;

            if (DepthSettingsPanel != null)
            {
                DepthSettingsPanel.IsEnabled = usesDepth;
                DepthSettingsPanel.Opacity = usesDepth ? 1.0 : 0.45;
            }

            if (TimePerMovePanel != null)
            {
                TimePerMovePanel.IsEnabled = usesMoveTime;
                TimePerMovePanel.Opacity = usesMoveTime ? 1.0 : 0.45;
            }

            if (GameClockPanel != null)
            {
                // The chess clock remains available in every mode; in Game Clock mode it also controls the AI budget.
                GameClockPanel.IsEnabled = true;
                GameClockPanel.Opacity = usesGameClock ? 1.0 : 0.75;
            }
        }

        //-----------------------------------------------------------------------
        private void ResetClockButton_Click(object sender, RoutedEventArgs e)
        {
            clockController?.Reset(GetConfiguredGameClockTime());
            StartTimerForCurrentPlayer();
            UpdateAnalysisFields(null);
            StartBackgroundPositionAnalysis("Clock reset");
        }

        //-----------------------------------------------------------------------
        private int GetConfiguredDepth()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(GetConfiguredDepth);
            }

            if (MinimaxDepthTextBox != null && int.TryParse(MinimaxDepthTextBox.Text, out int depth))
            {
                return Math.Max(1, Math.Min(depth, 18));
            }

            return Math.Max(1, MinimaxDepth);
        }

        //-----------------------------------------------------------------------
        private int GetConfiguredTimePerMoveMilliseconds()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(GetConfiguredTimePerMoveMilliseconds);
            }

            if (AiTimePerMoveTextBox != null && int.TryParse(AiTimePerMoveTextBox.Text, out int milliseconds))
            {
                return Math.Max(MinAiMoveTimeMilliseconds, Math.Min(milliseconds, MaxAiMoveTimeMilliseconds));
            }

            return DefaultAiMoveTimeMilliseconds;
        }

        //-----------------------------------------------------------------------
        private TimeSpan GetConfiguredGameClockTime()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(GetConfiguredGameClockTime);
            }

            if (GameClockMinutesTextBox != null && double.TryParse(GameClockMinutesTextBox.Text, out double minutes))
            {
                minutes = Math.Max(1, Math.Min(minutes, 60));
                return TimeSpan.FromMinutes(minutes);
            }

            return TimeSpan.FromMinutes(5);
        }

        //-----------------------------------------------------------------------
        private int GetGameClockSearchBudgetMilliseconds(int player)
        {
            TimeSpan remaining = clockController?.GetRemaining(player) ?? TimeSpan.Zero;
            int usableMilliseconds = Math.Max(MinAiMoveTimeMilliseconds, (int)remaining.TotalMilliseconds - GameClockSafetyMilliseconds);
            int suggestedMilliseconds = usableMilliseconds / GameClockMovesToGoEstimate;
            return Math.Max(MinAiMoveTimeMilliseconds, Math.Min(suggestedMilliseconds, MaxAiMoveTimeMilliseconds));
        }

        //-----------------------------------------------------------------------
        private SearchConfiguration CaptureSearchConfiguration(int player, bool analysisOnly = false)
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(() => CaptureSearchConfiguration(player, analysisOnly));
            }

            int configuredDepth = GetConfiguredDepth();
            int? timeLimitMilliseconds = null;
            int maximumDepth = configuredDepth;
            bool iterative = false;
            AiSearchMode mode = selectedAiSearchMode;

            if (mode == AiSearchMode.FixedTimePerMove)
            {
                timeLimitMilliseconds = GetConfiguredTimePerMoveMilliseconds();
                maximumDepth = MaxIterativeSearchDepth;
                iterative = true;
            }
            else if (mode == AiSearchMode.GameClock)
            {
                timeLimitMilliseconds = GetGameClockSearchBudgetMilliseconds(player);
                maximumDepth = MaxIterativeSearchDepth;
                iterative = true;
            }
            else if (analysisOnly)
            {
                maximumDepth = configuredDepth;
            }

            bool useLinearEvaluator = UseLinearEvaluatorCheckBox?.IsChecked == true && latestLinearModel != null;

            return new SearchConfiguration
            {
                Mode = mode,
                MaximumDepth = maximumDepth,
                TimeLimitMilliseconds = timeLimitMilliseconds,
                Iterative = iterative,
                ForcedCaptureComparisonDepth = configuredDepth,
                UseLinearEvaluator = useLinearEvaluator,
                LinearModel = latestLinearModel
            };
        }

        //-----------------------------------------------------------------------
        private void UpdateAnalysisFields(SearchResult? result)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateAnalysisFields(result));
                return;
            }

            string summary = result == null
                ? string.Empty
                : analysisPanelUpdater?.UpdateFromResult(result, GetSearchModeLabel(result.Mode));

            if (result == null)
            {
                analysisPanelUpdater?.Clear(GetSearchModeLabel(selectedAiSearchMode));
            }
            else if (!string.IsNullOrWhiteSpace(summary))
            {
                SetAnalysisSummary(summary);
            }
        }

        //-----------------------------------------------------------------------
        private void UpdateAnalysisProgress(WinDama.Core.SearchProgress progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateAnalysisProgress(progress)));
                return;
            }

            string summary = analysisPanelUpdater?.UpdateFromProgress(progress, GetSearchModeLabel(selectedAiSearchMode));
            if (!string.IsNullOrWhiteSpace(summary))
            {
                SetAnalysisSummary(summary);
            }
        }



        //-----------------------------------------------------------------------
        private string GetSearchModeLabel(AiSearchMode mode)
        {
            return mode switch
            {
                AiSearchMode.FixedTimePerMove => "Fixed time / move",
                AiSearchMode.GameClock => "Game clock",
                _ => "Fixed depth"
            };
        }

        //-----------------------------------------------------------------------
        private string GetSearchModeLabel(WinDama.Core.SearchMode mode)
        {
            return mode switch
            {
                WinDama.Core.SearchMode.FixedTimePerMove => "Fixed time / move",
                WinDama.Core.SearchMode.GameClock => "Game clock",
                _ => "Fixed depth"
            };
        }











        //-----------------------------------------------------------------------
        private void ResetGame()
        {
            gameStateVersion++;
            CancelBackgroundPositionAnalysis();
            gameController.NewGame(GameController.CreateInitialBoard(), startingPlayer: 1);
            gameController.SetGameMode(selectedGameMode);
            SynchronizeFieldsFromGameController();
            ClearMoveSelection(redraw: false);
            UpdateBoardUI();
            UndoStack.Clear();
            RedoStack.Clear();
            gameStateVersion++;
            clockController?.Reset(GetConfiguredGameClockTime());
            RefreshGameModeUi();
            UpdateAnalysisFields(null);
            StartTimerForCurrentPlayer();
            ScheduleAIMoveIfNeeded();
            StartBackgroundPositionAnalysis("New game");
        }




        //-----------------------------------------------------------------------
        private void BoardEditorCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (BoardEditorCheckBox == null || isRestoringSnapshot || gameController == null)
            {
                return;
            }

            isBoardEditorMode = BoardEditorCheckBox.IsChecked == true;
            ClearMoveSelection(redraw: false);
            gameStateVersion++;

            if (isBoardEditorMode)
            {
                StopAllTimers();
                CancelBackgroundPositionAnalysis();
                SetStatus("Board editor enabled. Choose a piece and click board squares to create a test position.");
                SetAnalysisSummary("Editor mode: rules are paused while editing; save/load positions as JSON test cases.");
            }
            else
            {
                SynchronizeEditedPosition("Board editor disabled", saveUndo: false, resetLastMove: true);
                StartTimerForCurrentPlayer();
                SetStatus("Board editor disabled. Position is ready; AI is paused until the next user action.");
            }

            RefreshEditorUiFromState();
            UpdateBoardUI();
        }

        //-----------------------------------------------------------------------
        private void EditorPieceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditorPieceComboBox == null)
            {
                return;
            }

            editorSelectedPieceValue = GetSelectedEditorPieceValue();
            RefreshEditorHint();
        }

        //-----------------------------------------------------------------------
        private void EditorSideToMoveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditorSideToMoveComboBox == null || isRestoringSnapshot || gameController == null || clockController == null)
            {
                return;
            }

            int newPlayer = EditorSideToMoveComboBox.SelectedIndex == 1 ? -1 : 1;
            if (newPlayer == currentPlayer)
            {
                return;
            }

            UndoStack.Push(CreateSnapshot());
            RedoStack.Clear();
            currentPlayer = newPlayer;
            SynchronizeEditedPosition("Side to move changed", saveUndo: false, resetLastMove: true);
        }

        //-----------------------------------------------------------------------
        private void EditorEmptyBoardButton_Click(object sender, RoutedEventArgs e)
        {
            UndoStack.Push(CreateSnapshot());
            RedoStack.Clear();
            board = new int[8, 8];
            currentPlayer = GetEditorSideToMove();
            SynchronizeEditedPosition("Empty test board", saveUndo: false, resetLastMove: true);
            SetStatus("Empty board created. Place pieces using editor mode.");
        }

        //-----------------------------------------------------------------------
        private void EditorStartPositionButton_Click(object sender, RoutedEventArgs e)
        {
            UndoStack.Push(CreateSnapshot());
            RedoStack.Clear();
            board = GameController.CreateInitialBoard();
            currentPlayer = 1;
            SetEditorSideToMove(1);
            SynchronizeEditedPosition("Starting position loaded into editor", saveUndo: false, resetLastMove: true);
        }

        //-----------------------------------------------------------------------
        private void SavePositionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(DefaultTestPositionsFolder);
                string suggestedName = BoardEditorController.BuildSafePositionFileName(PositionNameTextBox?.Text);
                SaveFileDialog dialog = new SaveFileDialog
                {
                    Title = "Save WinDama test position",
                    InitialDirectory = DefaultTestPositionsFolder,
                    FileName = suggestedName,
                    Filter = "WinDama test position (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog(this) != true)
                {
                    return;
                }

                TestPosition position = TestPosition.FromState(
                    PositionNameTextBox?.Text,
                    PositionNotesTextBox?.Text,
                    board,
                    currentPlayer,
                    selectedGameMode,
                    isGameOver);

                TestPositionStore.Save(dialog.FileName, position);
                SetStatus($"Saved test position: {System.IO.Path.GetFileName(dialog.FileName)}");
                SetAnalysisSummary($"Saved '{position.Name}' with {BoardEditorController.CountPieces(board)} pieces. Side to move: {GetPlayerLabel(currentPlayer)}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save the test position.\n\n{ex.Message}", "Save Position", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void LoadPositionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(DefaultTestPositionsFolder);
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Title = "Load WinDama test position",
                    InitialDirectory = DefaultTestPositionsFolder,
                    Filter = "WinDama test position (*.json)|*.json|All files (*.*)|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog(this) != true)
                {
                    return;
                }

                TestPosition position = TestPositionStore.Load(dialog.FileName);
                UndoStack.Push(CreateSnapshot());
                RedoStack.Clear();

                board = position.ToBoard();
                currentPlayer = position.CurrentPlayer;
                selectedGameMode = position.GameMode;
                isGameOver = position.IsGameOver;

                if (PositionNameTextBox != null)
                {
                    PositionNameTextBox.Text = position.Name;
                }

                if (PositionNotesTextBox != null)
                {
                    PositionNotesTextBox.Text = position.Notes;
                }

                SetGameModeComboBoxFromMode(selectedGameMode);
                SetEditorSideToMove(currentPlayer);
                SynchronizeEditedPosition($"Loaded test position '{position.Name}'", saveUndo: false, resetLastMove: true);
                SetStatus($"Loaded test position: {position.Name}. {GetPlayerLabel(currentPlayer)} to move.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load the test position.\n\n{ex.Message}", "Load Position", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void ApplyBoardEditorClick(int row, int column)
        {
            if (!BoardEditorController.IsInsideBoard(row, column))
            {
                return;
            }

            int newValue = editorSelectedPieceValue;
            if (newValue != 0 && !IsPlayableSquare(row, column))
            {
                SetStatus($"Pieces can only be placed on playable diagonal squares. Square ({row + 1}, {column + 1}) was ignored.");
                return;
            }

            if (board[row, column] == newValue)
            {
                SetStatus($"Square ({row + 1}, {column + 1}) already contains {BoardEditorController.DescribePieceValue(newValue)}.");
                return;
            }

            UndoStack.Push(CreateSnapshot());
            RedoStack.Clear();
            board[row, column] = newValue;
            currentPlayer = GetEditorSideToMove();
            SynchronizeEditedPosition($"Edited square ({row + 1}, {column + 1})", saveUndo: false, resetLastMove: true);
            SetStatus($"Set square ({row + 1}, {column + 1}) to {BoardEditorController.DescribePieceValue(newValue)}.");
        }

        //-----------------------------------------------------------------------
        private void SynchronizeEditedPosition(string reason, bool saveUndo, bool resetLastMove)
        {
            if (saveUndo)
            {
                UndoStack.Push(CreateSnapshot());
                RedoStack.Clear();
            }

            gameStateVersion++;
            StopAllTimers();
            CancelBackgroundPositionAnalysis();
            ClearMoveSelection(redraw: false);

            Move lastMoveForState = resetLastMove ? null : lastMove;
            gameController.SetGameMode(selectedGameMode);
            gameController.RestoreState(board, currentPlayer, isGameOver: false, lastMove: lastMoveForState);
            SynchronizeFieldsFromGameController();
            gameStateVersion++;

            RefreshGameModeUi();
            RefreshEditorUiFromState();
            UpdateAnalysisFields(null);
            UpdateBoardUI();
            StartBackgroundPositionAnalysis(reason);
        }

        //-----------------------------------------------------------------------
        private void RefreshEditorUiFromState()
        {
            isRestoringSnapshot = true;
            try
            {
                if (BoardEditorCheckBox != null)
                {
                    BoardEditorCheckBox.IsChecked = isBoardEditorMode;
                }

                boardEditorController?.SetSelectedPieceValue(editorSelectedPieceValue);
                SetEditorSideToMove(currentPlayer);
            }
            finally
            {
                isRestoringSnapshot = false;
            }

            RefreshEditorHint();
        }

        //-----------------------------------------------------------------------
        private void RefreshEditorHint()
        {
            boardEditorController?.UpdateHint(isBoardEditorMode, editorSelectedPieceValue, currentPlayer);
        }

        //-----------------------------------------------------------------------
        private int GetSelectedEditorPieceValue()
        {
            return boardEditorController?.GetSelectedPieceValue(editorSelectedPieceValue) ?? editorSelectedPieceValue;
        }

        //-----------------------------------------------------------------------
        private int GetEditorSideToMove()
        {
            return boardEditorController?.GetSideToMove() ?? 1;
        }

        //-----------------------------------------------------------------------
        private void SetEditorSideToMove(int player)
        {
            boardEditorController?.SetSideToMove(player);
        }

        //-----------------------------------------------------------------------
        private bool IsPlayableSquare(int row, int column)
        {
            return BoardEditorController.IsPlayableSquare(row, column);
        }

        //-----------------------------------------------------------------------
        private void SetGameModeComboBoxFromMode(GameMode mode)
        {
            if (GameModeComboBox == null)
            {
                return;
            }

            isRestoringSnapshot = true;
            try
            {
                GameModeComboBox.SelectedIndex = mode switch
                {
                    GameMode.HumanVsHuman => 1,
                    GameMode.AIVsAI => 2,
                    _ => 0
                };
            }
            finally
            {
                isRestoringSnapshot = false;
            }
        }

        //-----------------------------------------------------------------------
        // Board-editor helper methods moved to BoardEditorController.

        //-----------------------------------------------------------------------
        private void CancelBackgroundPositionAnalysis()
        {
            backgroundAnalysisRequestId++;
            CancellationTokenSource? oldCancellation = backgroundAnalysisCancellation;
            backgroundAnalysisCancellation = null;
            if (oldCancellation != null)
            {
                try
                {
                    oldCancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Safe to ignore; another callback may already have completed cleanup.
                }
            }
        }

        //-----------------------------------------------------------------------
        private async void StartBackgroundPositionAnalysis(string reason)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => StartBackgroundPositionAnalysis(reason)));
                return;
            }

            if (!isContinuousAnalysisEnabled || isBackgroundAnalysisPaused)
            {
                CancelBackgroundPositionAnalysis();
                UpdateAnalysisControlButtons();
                return;
            }

            CancelBackgroundPositionAnalysis();

            if (isGameOver || board == null || isAiMoveInProgress)
            {
                return;
            }

            // If the side to move is controlled by the AI, the real AI search will
            // already update the analysis panel. Avoid running two searches for the
            // same position.
            if (IsAiControlledPlayer(currentPlayer))
            {
                return;
            }

            int[,] searchBoard = (int[,])board.Clone();
            int searchPlayer = currentPlayer;
            long searchVersion = gameStateVersion;
            long analysisId = ++backgroundAnalysisRequestId;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            backgroundAnalysisCancellation = cancellation;

            int depth = BackgroundAnalysisInfiniteDepth;
            EvaluationBreakdown staticBreakdown = positionEvaluator.EvaluateBreakdown(searchBoard, searchPlayer);
            SetAnalysisSummary($"{reason}: continuous analysis for {GetPlayerLabel(searchPlayer)}. Static eval {staticBreakdown.Total} ({AnalysisTextFormatter.FormatEvaluationBreakdown(staticBreakdown)}). It will keep deepening until the position changes.");

            try
            {
                WinDama.Core.SearchOptions options = new WinDama.Core.SearchOptions
                {
                    Mode = WinDama.Core.SearchMode.FixedDepth,
                    MaximumDepth = depth,
                    TimeLimitMilliseconds = null,
                    Iterative = true,
                    TieBreakTolerance = 0,
                    RandomizeNearBestMoves = false,
                    ForcedCaptureComparisonDepth = GetConfiguredDepth(),
                    ProgressIntervalMilliseconds = 150,
                    CancellationToken = cancellation.Token,
                    Progress = progress => UpdateBackgroundAnalysisProgress(analysisId, searchVersion, progress)
                };

                SearchEngine activeEngine = CreateActiveSearchEngine();
                SearchResult result = await Task.Run(() => activeEngine.FindBestMove(searchBoard, searchPlayer, options));

                if (cancellation.IsCancellationRequested || analysisId != backgroundAnalysisRequestId || searchVersion != gameStateVersion)
                {
                    return;
                }

                UpdateAnalysisFields(result);
                if (SearchModeValueTextBlock != null)
                {
                    SearchModeValueTextBlock.Text = "Continuous";
                }
                if (SearchBudgetTextBlock != null)
                {
                    SearchBudgetTextBlock.Text = "∞ / until position changes";
                }

                SetAnalysisSummary($"Position analysis completed up to depth {result.CompletedDepth}: {GetPlayerLabel(searchPlayer)}, eval {result.BestEvaluation}, best {AnalysisTextFormatter.FormatMove(result.BestMove)}, static {staticBreakdown.Total}.");
            }
            catch (Exception ex)
            {
                if (!cancellation.IsCancellationRequested)
                {
                    SetAnalysisSummary($"Background analysis stopped: {ex.Message}");
                }
            }
            finally
            {
                if (backgroundAnalysisCancellation == cancellation)
                {
                    backgroundAnalysisCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        //-----------------------------------------------------------------------
        private void UpdateBackgroundAnalysisProgress(long analysisId, long searchVersion, WinDama.Core.SearchProgress progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateBackgroundAnalysisProgress(analysisId, searchVersion, progress)));
                return;
            }

            if (analysisId != backgroundAnalysisRequestId || searchVersion != gameStateVersion)
            {
                return;
            }

            UpdateAnalysisProgress(progress);
            if (SearchModeValueTextBlock != null)
            {
                SearchModeValueTextBlock.Text = "Continuous";
            }
            if (SearchBudgetTextBlock != null)
            {
                SearchBudgetTextBlock.Text = "∞ / until position changes";
            }

            string bestMoveText = progress.BestMove == null ? "-" : AnalysisTextFormatter.FormatMove(progress.BestMove);
            SetAnalysisSummary($"Continuous analysis: depth {progress.CurrentDepth}, completed {progress.CompletedDepth}, eval {progress.BestEvaluation}, nodes {progress.Nodes:N0}, N/s {AnalysisTextFormatter.FormatNodesPerSecond(progress.NodesPerSecond)}, TT hits/cut {progress.TranspositionHits:N0}/{progress.TranspositionCutoffs:N0}, best {bestMoveText}.");
        }



        //-----------------------------------------------------------------------
        private async void RunBenchmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (isBenchmarkRunning || isTuningRunning || isTournamentRunning)
            {
                return;
            }

            string positionsDirectory = ResolveBundledFolder("TestPositions");
            string profilesDirectory = ResolveBundledFolder("EvaluationWeights");

            if (!Directory.Exists(positionsDirectory))
            {
                MessageBox.Show($"Could not find tactical positions folder:\n{positionsDirectory}", "Tactical Benchmark", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(profilesDirectory))
            {
                MessageBox.Show($"Could not find evaluation profiles folder:\n{profilesDirectory}", "Tactical Benchmark", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TacticalBenchmarkOptions options = CreateBenchmarkOptionsFromUi();
            SetBenchmarkBusy(true);
            latestBenchmarkSummary = null;
            if (ExportBenchmarkButton != null)
            {
                ExportBenchmarkButton.IsEnabled = false;
            }

            if (BenchmarkStatusTextBlock != null)
            {
                BenchmarkStatusTextBlock.Text = $"Running benchmark: depth {options.Depth}, positions folder '{System.IO.Path.GetFileName(positionsDirectory)}', profiles folder '{System.IO.Path.GetFileName(profilesDirectory)}'" + (BenchmarkIncludeLinearEvaluatorCheckBox?.IsChecked == true && latestLinearModel != null ? ", including learned evaluator..." : "...");
            }

            try
            {
                CancelBackgroundPositionAnalysis();
                TacticalBenchmarkRunner runner = new TacticalBenchmarkRunner();
                IReadOnlyList<EvaluationProfile> profiles = LoadEvaluationProfilesForComparison(BenchmarkIncludeLinearEvaluatorCheckBox?.IsChecked == true);
                TacticalBenchmarkSummary summary = await Task.Run(() => runner.Run(positionsDirectory, profiles, options));
                latestBenchmarkSummary = summary;

                if (BenchmarkSummaryTextBlock != null)
                {
                    BenchmarkSummaryTextBlock.Text = FormatBenchmarkSummary(summary);
                }

                if (BenchmarkStatusTextBlock != null)
                {
                    BenchmarkStatusTextBlock.Text = $"Benchmark completed: {summary.ResultCount} results, {summary.ProfileCount} profiles, {summary.PositionCount} positions.";
                }

                if (ExportBenchmarkButton != null)
                {
                    ExportBenchmarkButton.IsEnabled = summary.ResultCount > 0;
                }
            }
            catch (Exception ex)
            {
                if (BenchmarkStatusTextBlock != null)
                {
                    BenchmarkStatusTextBlock.Text = "Benchmark failed.";
                }

                MessageBox.Show($"Could not run the tactical benchmark.\n\n{ex.Message}", "Tactical Benchmark", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBenchmarkBusy(false);
                StartBackgroundPositionAnalysis("Benchmark completed");
            }
        }

        //-----------------------------------------------------------------------
        private void ExportBenchmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (latestBenchmarkSummary == null || latestBenchmarkSummary.ResultCount == 0)
            {
                MessageBox.Show("Run a tactical benchmark first, then export the CSV report.", "Export Benchmark", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export tactical benchmark CSV",
                FileName = $"windama-tactical-benchmark-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                Filter = "CSV report (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                TacticalBenchmarkRunner.SaveCsv(latestBenchmarkSummary, dialog.FileName);
                if (BenchmarkStatusTextBlock != null)
                {
                    BenchmarkStatusTextBlock.Text = $"CSV exported: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the benchmark CSV.\n\n{ex.Message}", "Export Benchmark", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void BenchmarkFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            string positionsDirectory = ResolveBundledFolder("TestPositions");
            string profilesDirectory = ResolveBundledFolder("EvaluationWeights");

            MessageBox.Show(
                $"Tactical positions:\n{positionsDirectory}\n\nEvaluation profiles:\n{profilesDirectory}\n\nThese folders are copied to the build output by the WPF project file.",
                "Benchmark Folders",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        //-----------------------------------------------------------------------
        private TacticalBenchmarkOptions CreateBenchmarkOptionsFromUi()
        {
            return new TacticalBenchmarkOptions
            {
                Depth = ReadPositiveInt(BenchmarkDepthTextBox?.Text, 5, 1, 18),
                ForcedCaptureComparisonDepth = ReadPositiveInt(BenchmarkDepthTextBox?.Text, 5, 1, 18),
                MaximumPositions = ReadPositiveInt(BenchmarkMaxPositionsTextBox?.Text, int.MaxValue, 0, int.MaxValue),
                UseQuiescenceSearch = BenchmarkUseQuiescenceCheckBox?.IsChecked == true,
                MaxQuiescenceDepth = 8,
                UseTranspositionTable = true,
                UseKillerMoves = true,
                UseHistoryHeuristic = true,
                RandomizeNearBestMoves = false
            };
        }

        //-----------------------------------------------------------------------
        private static int ReadPositiveInt(string? text, int fallback, int minimum, int maximum)
        {
            if (!int.TryParse(text, out int value))
            {
                return fallback;
            }

            if (value == 0 && minimum == 0)
            {
                return int.MaxValue;
            }

            return Math.Max(minimum, Math.Min(value, maximum));
        }

        //-----------------------------------------------------------------------
        private static string ResolveBundledFolder(string folderName)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new[]
            {
                System.IO.Path.Combine(baseDirectory, folderName),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, "..", "..", "..", "..", folderName)),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, "..", "..", "..", folderName))
            };

            return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }

        //-----------------------------------------------------------------------
        private void SetBenchmarkBusy(bool busy)
        {
            isBenchmarkRunning = busy;
            if (RunBenchmarkButton != null)
            {
                RunBenchmarkButton.IsEnabled = !busy;
                RunBenchmarkButton.Content = busy ? "Running..." : "Run";
            }

            if (BenchmarkDepthTextBox != null)
            {
                BenchmarkDepthTextBox.IsEnabled = !busy;
            }

            if (BenchmarkMaxPositionsTextBox != null)
            {
                BenchmarkMaxPositionsTextBox.IsEnabled = !busy;
            }

            if (BenchmarkUseQuiescenceCheckBox != null)
            {
                BenchmarkUseQuiescenceCheckBox.IsEnabled = !busy;
            }

            if (BenchmarkIncludeLinearEvaluatorCheckBox != null)
            {
                BenchmarkIncludeLinearEvaluatorCheckBox.IsEnabled = !busy;
            }
        }

        //-----------------------------------------------------------------------
        private static string FormatBenchmarkSummary(TacticalBenchmarkSummary summary)
        {
            if (summary.ResultCount == 0)
            {
                return "No benchmark results.";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Profiles: {summary.ProfileCount} | Positions: {summary.PositionCount} | Results: {summary.ResultCount}");
            builder.AppendLine("Profile ranking:");

            int rank = 1;
            foreach (TacticalBenchmarkProfileSummary profile in summary.Profiles)
            {
                builder.AppendLine($"{rank}. {profile.ProfileName}: {profile.Score}/{profile.MaximumScore} ({profile.ScorePercent:0.0}%), perfect {profile.PerfectPositions}/{profile.ScoredPositions}, eval {profile.AverageEvaluation:0.0}, nodes {profile.AverageNodes:0}, q {profile.AverageQuiescenceNodes:0}");
                rank++;
            }

            builder.AppendLine();
            builder.AppendLine("Failed / partial samples:");
            foreach (TacticalBenchmarkPositionResult result in summary.Results
                .Where(r => r.HasExpectations && r.Score < r.MaximumScore)
                .Take(8))
            {
                builder.AppendLine($"- {result.ProfileName} | {result.PositionFile}: {result.Score}/{result.MaximumScore}, best {result.BestMove}, expected {result.ExpectedBestMove}, eval {result.Evaluation}; {result.ScoreDetails}");
            }

            if (!summary.Results.Any(r => r.HasExpectations && r.Score < r.MaximumScore))
            {
                builder.AppendLine("- No failures among positions with expected fields.");
            }

            return builder.ToString().TrimEnd();
        }

        //-----------------------------------------------------------------------
        private async void RunTuningButton_Click(object sender, RoutedEventArgs e)
        {
            if (isTuningRunning || isBenchmarkRunning || isTournamentRunning)
            {
                return;
            }

            string positionsDirectory = ResolveBundledFolder("TestPositions");
            string profilesDirectory = ResolveBundledFolder("EvaluationWeights");

            if (!Directory.Exists(positionsDirectory))
            {
                MessageBox.Show($"Could not find tactical positions folder:\n{positionsDirectory}", "Evaluation Tuner", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(profilesDirectory))
            {
                MessageBox.Show($"Could not find evaluation profiles folder:\n{profilesDirectory}", "Evaluation Tuner", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EvaluationProfile baseProfile;
            try
            {
                baseProfile = LoadBaseTuningProfile(profilesDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load the base evaluation profile.\n\n{ex.Message}", "Evaluation Tuner", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            EvaluationWeightTuningOptions options = CreateTuningOptionsFromUi();
            latestTuningSummary = null;
            SetTuningBusy(true);

            if (ExportBestTunedProfileButton != null)
            {
                ExportBestTunedProfileButton.IsEnabled = false;
            }

            if (ExportTuningCsvButton != null)
            {
                ExportTuningCsvButton.IsEnabled = false;
            }

            if (TuningStatusTextBlock != null)
            {
                TuningStatusTextBlock.Text = $"Tuning from '{baseProfile.Name}': {options.MaximumVariants} candidates, ±{options.VariationPercent}%, benchmark depth {options.BenchmarkOptions.Depth}...";
            }

            try
            {
                EvaluationWeightTuner tuner = new EvaluationWeightTuner();
                EvaluationWeightTuningSummary summary = await Task.Run(() => tuner.Tune(positionsDirectory, baseProfile, options));
                latestTuningSummary = summary;

                if (TuningSummaryTextBlock != null)
                {
                    TuningSummaryTextBlock.Text = FormatTuningSummary(summary);
                }

                if (TuningStatusTextBlock != null)
                {
                    EvaluationWeightTuningResult? best = summary.BestResult;
                    TuningStatusTextBlock.Text = best == null
                        ? "Tuning completed with no candidates."
                        : $"Tuning completed. Best: {best.CandidateName} = {best.Score}/{best.MaximumScore} ({best.ScorePercent:0.0}%).";
                }

                if (ExportBestTunedProfileButton != null)
                {
                    ExportBestTunedProfileButton.IsEnabled = summary.BestResult != null;
                }

                if (ExportTuningCsvButton != null)
                {
                    ExportTuningCsvButton.IsEnabled = summary.CandidateCount > 0;
                }
            }
            catch (Exception ex)
            {
                if (TuningStatusTextBlock != null)
                {
                    TuningStatusTextBlock.Text = "Tuning failed.";
                }

                MessageBox.Show($"Could not run the evaluation tuner.\n\n{ex.Message}", "Evaluation Tuner", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetTuningBusy(false);
                StartBackgroundPositionAnalysis("Evaluation tuning completed");
            }
        }

        //-----------------------------------------------------------------------
        private void ExportBestTunedProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (latestTuningSummary?.BestResult == null)
            {
                MessageBox.Show("Run the evaluation tuner first, then export the best profile.", "Export Tuned Profile", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export best tuned evaluation profile",
                FileName = $"windama-best-tuned-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                Filter = "Evaluation weights (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestTuningSummary.SaveBestWeights(dialog.FileName);
                if (TuningStatusTextBlock != null)
                {
                    TuningStatusTextBlock.Text = $"Best tuned profile exported: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the tuned profile.\n\n{ex.Message}", "Export Tuned Profile", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void ExportTuningCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (latestTuningSummary == null || latestTuningSummary.CandidateCount == 0)
            {
                MessageBox.Show("Run the evaluation tuner first, then export the tuning CSV.", "Export Tuning CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export evaluation tuning CSV",
                FileName = $"windama-evaluation-tuning-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                Filter = "CSV report (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                EvaluationWeightTuningSummary.SaveCsv(latestTuningSummary, dialog.FileName);
                if (TuningStatusTextBlock != null)
                {
                    TuningStatusTextBlock.Text = $"Tuning CSV exported: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the tuning CSV.\n\n{ex.Message}", "Export Tuning CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private EvaluationWeightTuningOptions CreateTuningOptionsFromUi()
        {
            TacticalBenchmarkOptions benchmarkOptions = CreateBenchmarkOptionsFromUi();
            benchmarkOptions = new TacticalBenchmarkOptions
            {
                Depth = benchmarkOptions.Depth,
                ForcedCaptureComparisonDepth = benchmarkOptions.ForcedCaptureComparisonDepth,
                MaximumPositions = ReadPositiveInt(TuningMaxPositionsTextBox?.Text, benchmarkOptions.MaximumPositions, 0, int.MaxValue),
                UseQuiescenceSearch = benchmarkOptions.UseQuiescenceSearch,
                MaxQuiescenceDepth = benchmarkOptions.MaxQuiescenceDepth,
                UseTranspositionTable = benchmarkOptions.UseTranspositionTable,
                UseKillerMoves = benchmarkOptions.UseKillerMoves,
                UseHistoryHeuristic = benchmarkOptions.UseHistoryHeuristic,
                RandomizeNearBestMoves = false
            };

            return new EvaluationWeightTuningOptions
            {
                BenchmarkOptions = benchmarkOptions,
                VariationPercent = ReadPositiveInt(TuningVariationTextBox?.Text, 15, 1, 100),
                MaximumVariants = ReadPositiveInt(TuningMaxVariantsTextBox?.Text, 32, 1, 256),
                IncludeBaseProfile = true
            };
        }

        //-----------------------------------------------------------------------
        private static EvaluationProfile LoadBaseTuningProfile(string profilesDirectory)
        {
            IReadOnlyList<EvaluationProfile> profiles = EvaluationProfileStore.LoadProfiles(profilesDirectory);
            EvaluationProfile? balanced = profiles.FirstOrDefault(profile => profile.Name.Contains("balanced", StringComparison.OrdinalIgnoreCase));
            return balanced ?? profiles.First();
        }

        //-----------------------------------------------------------------------
        private void SetTuningBusy(bool busy)
        {
            isTuningRunning = busy;
            if (RunTuningButton != null)
            {
                RunTuningButton.IsEnabled = !busy;
                RunTuningButton.Content = busy ? "Tuning..." : "Tune";
            }

            if (TuningVariationTextBox != null)
            {
                TuningVariationTextBox.IsEnabled = !busy;
            }

            if (TuningMaxVariantsTextBox != null)
            {
                TuningMaxVariantsTextBox.IsEnabled = !busy;
            }

            if (TuningMaxPositionsTextBox != null)
            {
                TuningMaxPositionsTextBox.IsEnabled = !busy;
            }
        }

        //-----------------------------------------------------------------------
        private static string FormatTuningSummary(EvaluationWeightTuningSummary summary)
        {
            if (summary.CandidateCount == 0)
            {
                return "No tuning candidates.";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Base: {summary.BaseProfileName} | Candidates: {summary.CandidateCount}");
            builder.AppendLine("Tuned ranking:");

            int rank = 1;
            foreach (EvaluationWeightTuningResult result in summary.RankedResults.Take(12))
            {
                string change = result.ChangedWeightName == "base"
                    ? "base"
                    : $"{result.ChangedWeightName} {result.OriginalValue}->{result.TunedValue}";

                builder.AppendLine($"{rank}. {change}: {result.Score}/{result.MaximumScore} ({result.ScorePercent:0.0}%), perfect {result.PerfectPositions}/{result.ScoredPositions}, nodes {result.AverageNodes:0}");
                rank++;
            }

            EvaluationWeightTuningResult? best = summary.BestResult;
            if (best != null)
            {
                builder.AppendLine();
                builder.AppendLine($"Best candidate: {best.CandidateName}");
            }

            return builder.ToString().TrimEnd();
        }


        //-----------------------------------------------------------------------
        private void LoadTournamentProfileSelectors()
        {
            try
            {
                IReadOnlyList<EvaluationProfile> profiles = LoadEvaluationProfilesForComparison(includeLearned: true);
                List<string> names = profiles.Select(profile => profile.Name).ToList();

                if (TournamentProfileAComboBox != null)
                {
                    string? oldSelection = TournamentProfileAComboBox.SelectedItem?.ToString();
                    TournamentProfileAComboBox.ItemsSource = names;
                    TournamentProfileAComboBox.SelectedItem = names.FirstOrDefault(name => string.Equals(name, oldSelection, StringComparison.OrdinalIgnoreCase));
                    if (TournamentProfileAComboBox.SelectedIndex < 0)
                    {
                        TournamentProfileAComboBox.SelectedIndex = names.Count > 0 ? 0 : -1;
                    }
                }

                if (TournamentProfileBComboBox != null)
                {
                    string? oldSelection = TournamentProfileBComboBox.SelectedItem?.ToString();
                    TournamentProfileBComboBox.ItemsSource = names;
                    TournamentProfileBComboBox.SelectedItem = names.FirstOrDefault(name => string.Equals(name, oldSelection, StringComparison.OrdinalIgnoreCase));
                    if (TournamentProfileBComboBox.SelectedIndex < 0)
                    {
                        TournamentProfileBComboBox.SelectedIndex = names.Count > 1 ? 1 : (names.Count > 0 ? 0 : -1);
                    }
                }
            }
            catch
            {
                // The rest of the application must remain usable even if bundled
                // profile folders are missing during development.
            }
        }

        //-----------------------------------------------------------------------
        private async void RunTournamentButton_Click(object sender, RoutedEventArgs e)
        {
            if (isTournamentRunning || isBenchmarkRunning || isTuningRunning)
            {
                return;
            }

            IReadOnlyList<EvaluationProfile> profiles;
            try
            {
                profiles = LoadEvaluationProfilesForComparison(includeLearned: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load evaluation profiles.\n\n{ex.Message}", "Profile Tournament", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            EvaluationProfile? profileA = FindSelectedTournamentProfile(profiles, TournamentProfileAComboBox?.SelectedItem?.ToString());
            EvaluationProfile? profileB = FindSelectedTournamentProfile(profiles, TournamentProfileBComboBox?.SelectedItem?.ToString());
            if (profileA == null || profileB == null)
            {
                MessageBox.Show("Select two evaluation profiles first.", "Profile Tournament", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            EvaluationTournamentOptions options = CreateTournamentOptionsFromUi();
            latestTournamentSummary = null;
            SetTournamentBusy(true);
            if (ExportTournamentCsvButton != null)
            {
                ExportTournamentCsvButton.IsEnabled = false;
            }
            if (ExportTournamentLogButton != null)
            {
                ExportTournamentLogButton.IsEnabled = false;
            }
            if (TournamentStatusTextBlock != null)
            {
                TournamentStatusTextBlock.Text = $"Running tournament: {profileA.Name} vs {profileB.Name}, depth {options.Depth}, {options.GamesPerColor} game(s)/color...";
            }

            try
            {
                CancelBackgroundPositionAnalysis();
                EvaluationTournamentRunner runner = new EvaluationTournamentRunner();
                EvaluationTournamentSummary summary = await Task.Run(() => runner.Run(profileA, profileB, options));
                latestTournamentSummary = summary;
                latestGameDatabase = GameDatabase.FromTournament(summary);
                latestPositionDataset = null;
                openingBook = OpeningBook.Build(latestGameDatabase, ReadPositiveInt(OpeningBookMaxPliesTextBox?.Text, 16, 1, 80));
                UpdateOpeningBookSummary($"Tournament completed and book built from {summary.GameCount} game(s).");
                UpdateDatasetSummary($"Tournament completed. Build dataset from {latestGameDatabase.TotalPlies} plies.");

                if (TournamentSummaryTextBlock != null)
                {
                    TournamentSummaryTextBlock.Text = FormatTournamentSummary(summary);
                }
                if (TournamentStatusTextBlock != null)
                {
                    TournamentStatusTextBlock.Text = $"Tournament completed: {summary.GameCount} game(s).";
                }
                if (ExportTournamentCsvButton != null)
                {
                    ExportTournamentCsvButton.IsEnabled = summary.GameCount > 0;
                }
                if (ExportTournamentLogButton != null)
                {
                    ExportTournamentLogButton.IsEnabled = summary.GameCount > 0;
                }
            }
            catch (Exception ex)
            {
                if (TournamentStatusTextBlock != null)
                {
                    TournamentStatusTextBlock.Text = "Tournament failed.";
                }

                MessageBox.Show($"Could not run the profile tournament.\n\n{ex.Message}", "Profile Tournament", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetTournamentBusy(false);
                StartBackgroundPositionAnalysis("Profile tournament completed");
            }
        }

        //-----------------------------------------------------------------------
        private void ExportTournamentCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (latestTournamentSummary == null || latestTournamentSummary.GameCount == 0)
            {
                MessageBox.Show("Run a profile tournament first, then export the CSV report.", "Export Tournament", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export profile tournament CSV",
                FileName = $"windama-profile-tournament-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                Filter = "CSV report (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestTournamentSummary.SaveCsv(dialog.FileName);
                if (TournamentStatusTextBlock != null)
                {
                    TournamentStatusTextBlock.Text = $"Tournament CSV exported: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the tournament CSV.\n\n{ex.Message}", "Export Tournament", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void ExportTournamentLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (latestTournamentSummary == null || latestTournamentSummary.GameCount == 0)
            {
                MessageBox.Show("Run a profile tournament first, then export the game log.", "Export Tournament", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export profile tournament game log",
                FileName = $"windama-profile-tournament-games-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                Filter = "Text log (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestTournamentSummary.SaveGameLog(dialog.FileName);
                if (TournamentStatusTextBlock != null)
                {
                    TournamentStatusTextBlock.Text = $"Tournament game log exported: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the tournament game log.\n\n{ex.Message}", "Export Tournament", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private EvaluationTournamentOptions CreateTournamentOptionsFromUi()
        {
            TacticalBenchmarkOptions benchmarkOptions = CreateBenchmarkOptionsFromUi();
            return new EvaluationTournamentOptions
            {
                Depth = benchmarkOptions.Depth,
                ForcedCaptureComparisonDepth = benchmarkOptions.ForcedCaptureComparisonDepth,
                UseQuiescenceSearch = benchmarkOptions.UseQuiescenceSearch,
                MaxQuiescenceDepth = benchmarkOptions.MaxQuiescenceDepth,
                UseTranspositionTable = benchmarkOptions.UseTranspositionTable,
                UseKillerMoves = benchmarkOptions.UseKillerMoves,
                UseHistoryHeuristic = benchmarkOptions.UseHistoryHeuristic,
                RandomizeNearBestMoves = false,
                GamesPerColor = ReadPositiveInt(TournamentGamesPerColorTextBox?.Text, 1, 1, 20),
                MaxPliesPerGame = ReadPositiveInt(TournamentMaxPliesTextBox?.Text, 120, 20, 1000),
                MaxMovesWithoutCaptureOrPromotion = 80
            };
        }

        private IReadOnlyList<EvaluationProfile> LoadEvaluationProfilesForComparison(bool includeLearned)
        {
            string profilesDirectory = ResolveBundledFolder("EvaluationWeights");
            List<EvaluationProfile> profiles = Directory.Exists(profilesDirectory)
                ? EvaluationProfileStore.LoadProfiles(profilesDirectory).ToList()
                : new List<EvaluationProfile>();

            if (includeLearned && latestLinearModel != null)
            {
                string learnedName = $"learned: {latestLinearModel.Name}";
                if (!profiles.Any(profile => string.Equals(profile.Name, learnedName, StringComparison.OrdinalIgnoreCase)))
                {
                    profiles.Add(new EvaluationProfile(learnedName, latestLinearModel));
                }
            }

            if (profiles.Count == 0)
            {
                profiles.Add(new EvaluationProfile("default", EvaluationWeights.Default));
            }

            return profiles;
        }

        //-----------------------------------------------------------------------
        private static EvaluationProfile? FindSelectedTournamentProfile(IReadOnlyList<EvaluationProfile> profiles, string? selectedName)
        {
            if (profiles == null || profiles.Count == 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(selectedName))
            {
                return profiles.First();
            }

            return profiles.FirstOrDefault(profile => string.Equals(profile.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                ?? profiles.FirstOrDefault(profile => selectedName.Contains(profile.Name, StringComparison.OrdinalIgnoreCase))
                ?? profiles.First();
        }

        //-----------------------------------------------------------------------
        private void SetTournamentBusy(bool busy)
        {
            isTournamentRunning = busy;
            if (RunTournamentButton != null)
            {
                RunTournamentButton.IsEnabled = !busy;
                RunTournamentButton.Content = busy ? "Running..." : "Run";
            }
            if (TournamentProfileAComboBox != null)
            {
                TournamentProfileAComboBox.IsEnabled = !busy;
            }
            if (TournamentProfileBComboBox != null)
            {
                TournamentProfileBComboBox.IsEnabled = !busy;
            }
            if (TournamentGamesPerColorTextBox != null)
            {
                TournamentGamesPerColorTextBox.IsEnabled = !busy;
            }
            if (TournamentMaxPliesTextBox != null)
            {
                TournamentMaxPliesTextBox.IsEnabled = !busy;
            }
        }

        //-----------------------------------------------------------------------
        private static string FormatTournamentSummary(EvaluationTournamentSummary summary)
        {
            if (summary.GameCount == 0)
            {
                return "No tournament games.";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Tournament: {summary.ProfileA} vs {summary.ProfileB}");
            builder.AppendLine($"Games: {summary.GameCount}");
            builder.AppendLine("Ranking:");

            int rank = 1;
            foreach (EvaluationTournamentProfileSummary profile in summary.Profiles)
            {
                builder.AppendLine($"{rank}. {profile.ProfileName}: {profile.Points:0.0}/{profile.Games} ({profile.ScorePercent:0.0}%) W{profile.Wins} D{profile.Draws} L{profile.Losses}, avg ply {profile.AveragePlyCount:0.0}");
                rank++;
            }

            builder.AppendLine();
            builder.AppendLine("Games:");
            foreach (EvaluationTournamentGameResult game in summary.Games.Take(8))
            {
                builder.AppendLine($"G{game.GameNumber}: {game.PlayerOneProfile} vs {game.PlayerTwoProfile} {game.ResultText}, plies {game.PlyCount}, nodes {game.Nodes:N0}");
            }

            return builder.ToString().TrimEnd();
        }

        //-----------------------------------------------------------------------
        private void LoadBundledBookButton_Click(object sender, RoutedEventArgs e)
        {
            TryLoadBundledOpeningBook(showMessageOnError: true);
        }

        //-----------------------------------------------------------------------
        private bool TryLoadBundledOpeningBook(bool showMessageOnError)
        {
            try
            {
                string? folder = FindBundledOpeningBooksFolder();
                if (folder == null)
                {
                    string message = "The built-in OpeningBooks folder was not found in the application directory.";
                    UpdateOpeningBookSummary(message);
                    if (showMessageOnError)
                    {
                        MessageBox.Show(message, "Opening Book", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return false;
                }

                string[] files = Directory.GetFiles(folder, "*.ouv", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (files.Length == 0)
                {
                    string message = $"No .ouv files were found in the built-in folder: {folder}";
                    UpdateOpeningBookSummary(message);
                    if (showMessageOnError)
                    {
                        MessageBox.Show(message, "Opening Book", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return false;
                }

                latestGameDatabase = OuvOpeningBookImporter.ImportFiles(files, includeMirroredLines: true);
                latestPositionDataset = null;
                int maxPlies = ReadPositiveInt(OpeningBookMaxPliesTextBox?.Text, 16, 1, 120);
                openingBook = OpeningBook.Build(latestGameDatabase, maxPlies);

                if (UseOpeningBookCheckBox != null)
                {
                    UseOpeningBookCheckBox.IsChecked = true;
                }

                UpdateDatasetSummary($"Built-in opening database loaded with {latestGameDatabase.TotalPlies} plies. Build dataset when ready.");
                UpdateOpeningBookSummary($"Built-in opening book loaded automatically from {files.Length} .ouv file(s): {latestGameDatabase.GameCount} line(s, including mirrored lines).");
                return true;
            }
            catch (Exception ex)
            {
                UpdateOpeningBookSummary($"Built-in opening book could not be loaded: {ex.Message}");
                if (showMessageOnError)
                {
                    MessageBox.Show($"Could not load the built-in opening book.\n\n{ex.Message}", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return false;
            }
        }

        //-----------------------------------------------------------------------
        private static string? FindBundledOpeningBooksFolder()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string currentDirectory = Environment.CurrentDirectory;

            string[] candidates =
            {
                System.IO.Path.Combine(baseDirectory, BundledOpeningBooksFolderName),
                System.IO.Path.Combine(currentDirectory, BundledOpeningBooksFolderName),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, "..", "..", "..", "..", BundledOpeningBooksFolderName)),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(currentDirectory, BundledOpeningBooksFolderName))
            };

            return candidates.FirstOrDefault(Directory.Exists);
        }

        //-----------------------------------------------------------------------
        private void BuildOpeningBookButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (latestGameDatabase == null)
                {
                    if (latestTournamentSummary == null || latestTournamentSummary.GameCount == 0)
                    {
                        MessageBox.Show("Run a profile tournament first or load a saved game database.", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    latestGameDatabase = GameDatabase.FromTournament(latestTournamentSummary);
                }

                int maxPlies = ReadPositiveInt(OpeningBookMaxPliesTextBox?.Text, 16, 1, 80);
                openingBook = OpeningBook.Build(latestGameDatabase, maxPlies);
                UpdateOpeningBookSummary($"Book built from {latestGameDatabase.GameCount} game(s), max plies {maxPlies}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not build the opening book.\n\n{ex.Message}", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void ImportOuvBookButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Import legacy WinDamas .ouv opening file",
                Filter = "WinDamas opening files (*.ouv)|*.ouv|All files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestGameDatabase = OuvOpeningBookImporter.ImportFiles(dialog.FileNames, includeMirroredLines: true);
                latestPositionDataset = null;
                int maxPlies = ReadPositiveInt(OpeningBookMaxPliesTextBox?.Text, 16, 1, 120);
                openingBook = OpeningBook.Build(latestGameDatabase, maxPlies);
                UpdateDatasetSummary($"Imported .ouv database with {latestGameDatabase.TotalPlies} plies. Build dataset when ready.");
                UpdateOpeningBookSummary($"Imported {dialog.FileNames.Length} .ouv file(s): {latestGameDatabase.GameCount} opening line(s, including mirrored lines), book built.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not import the .ouv opening file.\n\n{ex.Message}", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void LoadGameDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Load WinDama game database",
                Filter = "WinDama game database (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestGameDatabase = GameDatabase.Load(dialog.FileName);
                latestPositionDataset = null;
                int maxPlies = ReadPositiveInt(OpeningBookMaxPliesTextBox?.Text, 16, 1, 80);
                openingBook = OpeningBook.Build(latestGameDatabase, maxPlies);
                UpdateDatasetSummary($"Loaded database with {latestGameDatabase.TotalPlies} plies. Build dataset when ready.");
                UpdateOpeningBookSummary($"Loaded database '{System.IO.Path.GetFileName(dialog.FileName)}' and built book.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load the game database.\n\n{ex.Message}", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void SaveGameDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (latestGameDatabase == null || latestGameDatabase.GameCount == 0)
            {
                MessageBox.Show("Build or load a game database first.", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save WinDama game database",
                Filter = "WinDama game database (*.json)|*.json|All files (*.*)|*.*",
                FileName = "windama-games.json"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestGameDatabase.Save(dialog.FileName);
                UpdateOpeningBookSummary($"Game database saved: {System.IO.Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save the game database.\n\n{ex.Message}", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void ExportOpeningBookButton_Click(object sender, RoutedEventArgs e)
        {
            if (openingBook == null || openingBook.EntryCount == 0)
            {
                MessageBox.Show("Build or load an opening book first.", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export opening book CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "windama-opening-book.csv"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                openingBook.SaveCsv(dialog.FileName);
                UpdateOpeningBookSummary($"Opening book CSV exported: {System.IO.Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the opening book.\n\n{ex.Message}", "Opening Book", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void OpeningBookStatusButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateOpeningBookSummary("Opening book status refreshed.");
        }

        //-----------------------------------------------------------------------
        private void ClearOpeningBookButton_Click(object sender, RoutedEventArgs e)
        {
            openingBook = null;
            latestGameDatabase = null;
            latestPositionDataset = null;
            UpdateOpeningBookSummary("Opening book and loaded game database cleared. Click Built-in to reload the bundled book.");
            UpdateDatasetSummary("Game database cleared. No dataset available.");
        }

        //-----------------------------------------------------------------------
        private bool TryGetOpeningBookMove(int[,] sourceBoard, int player, IReadOnlyList<Move> legalMoves, out Move? move, out string reason)
        {
            move = null;
            reason = string.Empty;

            if (UseOpeningBookCheckBox?.IsChecked != true || openingBook == null || legalMoves.Count == 0)
            {
                return false;
            }

            int maxPlies = ReadPositiveInt(OpeningBookMaxPliesTextBox?.Text, 16, 1, 80);
            if (gameController.MoveHistory.Count >= maxPlies)
            {
                return false;
            }

            OpeningBookMode mode = GetOpeningBookModeFromUi();
            int minimumGames = ReadPositiveInt(OpeningBookMinGamesTextBox?.Text, 1, 1, 10000);
            OpeningBookRecommendation? recommendation = openingBook.RecommendMove(sourceBoard, player, legalMoves, mode, minimumGames);
            if (recommendation == null)
            {
                return false;
            }

            move = recommendation.Move;
            reason = recommendation.Reason;
            return true;
        }

        //-----------------------------------------------------------------------
        private OpeningBookMode GetOpeningBookModeFromUi()
        {
            return OpeningBookModeComboBox?.SelectedIndex switch
            {
                1 => OpeningBookMode.MostPlayed,
                2 => OpeningBookMode.Exploration,
                _ => OpeningBookMode.BestScore
            };
        }

        //-----------------------------------------------------------------------
        private void UpdateOpeningBookSummary(string status)
        {
            int gameCount = latestGameDatabase?.GameCount ?? 0;
            int totalPlies = latestGameDatabase?.TotalPlies ?? 0;
            int entryCount = openingBook?.EntryCount ?? 0;
            int positionCount = openingBook?.PositionCount ?? 0;

            if (OpeningBookStatusTextBlock != null)
            {
                OpeningBookStatusTextBlock.Text = status;
            }

            if (OpeningBookSummaryTextBlock != null)
            {
                OpeningBookSummaryTextBlock.Text =
                    $"DB games: {gameCount}, plies: {totalPlies}\n" +
                    $"Book positions: {positionCount}, entries: {entryCount}\n" +
                    $"Use book: {(UseOpeningBookCheckBox?.IsChecked == true ? "yes" : "no")}, mode: {GetOpeningBookModeFromUi()}";
            }

            if (SaveGameDatabaseButton != null)
            {
                SaveGameDatabaseButton.IsEnabled = gameCount > 0;
            }

            if (ExportOpeningBookButton != null)
            {
                ExportOpeningBookButton.IsEnabled = entryCount > 0;
            }

            UpdateEngineStatusBar();
        }

        //-----------------------------------------------------------------------
        private async void BuildDatasetButton_Click(object sender, RoutedEventArgs e)
        {
            if (isDatasetExportRunning)
            {
                return;
            }

            if (latestGameDatabase == null || latestGameDatabase.GameCount == 0)
            {
                MessageBox.Show("Run a profile tournament or load a game database first.", "Position Dataset", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int maxSamples = ReadPositiveInt(DatasetMaxSamplesTextBox?.Text, 0, 0, int.MaxValue);
            SetDatasetBusy(true);
            UpdateDatasetSummary($"Building dataset from {latestGameDatabase.GameCount} game(s)...");

            try
            {
                PositionDatasetExporter exporter = new PositionDatasetExporter(moveGenerator);
                PositionDataset dataset = await Task.Run(() => exporter.FromGameDatabase(latestGameDatabase, maxSamples));
                latestPositionDataset = dataset;
                UpdateDatasetSummary($"Dataset built: {dataset.SampleCount} sample(s).");
            }
            catch (Exception ex)
            {
                UpdateDatasetSummary("Dataset build failed.");
                MessageBox.Show($"Could not build the position dataset.\n\n{ex.Message}", "Position Dataset", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetDatasetBusy(false);
            }
        }

        //-----------------------------------------------------------------------
        private void ExportDatasetJsonlButton_Click(object sender, RoutedEventArgs e)
        {
            ExportPositionDataset("JSON Lines dataset (*.jsonl)|*.jsonl|All files (*.*)|*.*", ".jsonl", "windama-positions.jsonl", (dataset, path) => dataset.SaveJsonLines(path));
        }

        //-----------------------------------------------------------------------
        private void ExportDatasetCsvButton_Click(object sender, RoutedEventArgs e)
        {
            ExportPositionDataset("CSV dataset (*.csv)|*.csv|All files (*.*)|*.*", ".csv", "windama-positions.csv", (dataset, path) => dataset.SaveCsv(path));
        }

        //-----------------------------------------------------------------------
        private void ExportDatasetJsonButton_Click(object sender, RoutedEventArgs e)
        {
            ExportPositionDataset("JSON dataset (*.json)|*.json|All files (*.*)|*.*", ".json", "windama-positions.json", (dataset, path) => dataset.SaveJson(path));
        }

        //-----------------------------------------------------------------------
        private void ExportPositionDataset(string filter, string defaultExtension, string fileName, Action<PositionDataset, string> saveAction)
        {
            if (latestPositionDataset == null || latestPositionDataset.SampleCount == 0)
            {
                MessageBox.Show("Build a position dataset first.", "Position Dataset", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export WinDama position dataset",
                Filter = filter,
                FileName = fileName,
                DefaultExt = defaultExtension,
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                saveAction(latestPositionDataset, dialog.FileName);
                UpdateDatasetSummary($"Dataset exported: {System.IO.Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the position dataset.\n\n{ex.Message}", "Position Dataset", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void SetDatasetBusy(bool busy)
        {
            isDatasetExportRunning = busy;
            if (BuildDatasetButton != null)
            {
                BuildDatasetButton.IsEnabled = !busy;
                BuildDatasetButton.Content = busy ? "Building..." : "Build";
            }

            bool hasDataset = !busy && latestPositionDataset != null && latestPositionDataset.SampleCount > 0;
            if (ExportDatasetJsonlButton != null)
            {
                ExportDatasetJsonlButton.IsEnabled = hasDataset;
            }

            if (ExportDatasetCsvButton != null)
            {
                ExportDatasetCsvButton.IsEnabled = hasDataset;
            }

            if (ExportDatasetJsonButton != null)
            {
                ExportDatasetJsonButton.IsEnabled = hasDataset;
            }
        }

        //-----------------------------------------------------------------------
        private void UpdateDatasetSummary(string status)
        {
            int gameCount = latestGameDatabase?.GameCount ?? 0;
            int totalPlies = latestGameDatabase?.TotalPlies ?? 0;
            int sampleCount = latestPositionDataset?.SampleCount ?? 0;

            if (DatasetStatusTextBlock != null)
            {
                DatasetStatusTextBlock.Text = status;
            }

            if (DatasetSummaryTextBlock != null)
            {
                DatasetSummaryTextBlock.Text =
                    $"DB games: {gameCount}, plies: {totalPlies}\n" +
                    $"Samples: {sampleCount}\n" +
                    "Exports: JSONL for ML pipelines, CSV for inspection, JSON for reload.";
            }

            SetDatasetBusy(isDatasetExportRunning);
        }


        //-----------------------------------------------------------------------
        private async void TrainLinearEvaluatorButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLinearTrainingRunning)
            {
                return;
            }

            if (latestPositionDataset == null || latestPositionDataset.SampleCount == 0)
            {
                MessageBox.Show("Build a position dataset first, then train the linear evaluator.", "Linear Evaluator", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int maxSamples = ReadPositiveInt(LinearMaxSamplesTextBox?.Text, 0, 0, int.MaxValue);
            double l2 = ReadPositiveDouble(LinearL2TextBox?.Text, 0.001, 0.0, 1000.0);
            SetLinearEvaluatorBusy(true);
            UpdateLinearEvaluatorSummary($"Training linear evaluator on {latestPositionDataset.SampleCount} sample(s)...");

            try
            {
                LinearEvaluationTrainingOptions options = new LinearEvaluationTrainingOptions
                {
                    MaximumSamples = maxSamples,
                    L2Regularization = l2,
                    ModelName = "learned-linear-from-dataset",
                    TrainingSource = latestPositionDataset.Name
                };

                LinearEvaluationTrainer trainer = new LinearEvaluationTrainer();
                LinearEvaluationTrainingResult result = await Task.Run(() => trainer.Train(latestPositionDataset, options));
                latestLinearTrainingResult = result;
                latestLinearModel = result.Model;
                LoadTournamentProfileSelectors();
                UpdateLinearEvaluatorSummary($"Training completed. {result.Summary}");
                StartBackgroundPositionAnalysis("Linear evaluator trained");
            }
            catch (Exception ex)
            {
                UpdateLinearEvaluatorSummary("Linear evaluator training failed.");
                MessageBox.Show($"Could not train the linear evaluator.\n\n{ex.Message}", "Linear Evaluator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetLinearEvaluatorBusy(false);
            }
        }

        //-----------------------------------------------------------------------
        private void LoadLinearEvaluatorButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Load WinDama linear evaluator",
                Filter = "Linear evaluator JSON (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestLinearModel = LinearEvaluationModel.Load(dialog.FileName);
                latestLinearTrainingResult = null;
                LoadTournamentProfileSelectors();
                UpdateLinearEvaluatorSummary($"Loaded model: {latestLinearModel.Name}");
                StartBackgroundPositionAnalysis("Linear evaluator loaded");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load the linear evaluator.\n\n{ex.Message}", "Linear Evaluator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetLinearEvaluatorBusy(false);
            }
        }

        //-----------------------------------------------------------------------
        private void ExportLinearEvaluatorButton_Click(object sender, RoutedEventArgs e)
        {
            if (latestLinearModel == null)
            {
                MessageBox.Show("Train or load a linear evaluator first.", "Linear Evaluator", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export WinDama linear evaluator",
                Filter = "Linear evaluator JSON (*.json)|*.json|All files (*.*)|*.*",
                FileName = latestLinearModel.Name + ".json",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                latestLinearModel.Save(dialog.FileName);
                UpdateLinearEvaluatorSummary($"Model exported: {System.IO.Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export the linear evaluator.\n\n{ex.Message}", "Linear Evaluator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //-----------------------------------------------------------------------
        private void SetLinearEvaluatorBusy(bool busy)
        {
            isLinearTrainingRunning = busy;
            if (TrainLinearEvaluatorButton != null)
            {
                TrainLinearEvaluatorButton.IsEnabled = !busy;
                TrainLinearEvaluatorButton.Content = busy ? "Training..." : "Train";
            }

            if (LoadLinearEvaluatorButton != null)
            {
                LoadLinearEvaluatorButton.IsEnabled = !busy;
            }

            if (LinearMaxSamplesTextBox != null)
            {
                LinearMaxSamplesTextBox.IsEnabled = !busy;
            }

            if (LinearL2TextBox != null)
            {
                LinearL2TextBox.IsEnabled = !busy;
            }

            if (ExportLinearEvaluatorButton != null)
            {
                ExportLinearEvaluatorButton.IsEnabled = !busy && latestLinearModel != null;
            }
        }

        //-----------------------------------------------------------------------
        private void UpdateLinearEvaluatorSummary(string status)
        {
            if (LinearEvaluatorStatusTextBlock != null)
            {
                LinearEvaluatorStatusTextBlock.Text = status;
            }

            if (LinearEvaluatorSummaryTextBlock != null)
            {
                if (latestLinearModel == null)
                {
                    LinearEvaluatorSummaryTextBlock.Text = "Model: none";
                }
                else
                {
                    string accuracy = latestLinearTrainingResult == null
                        ? "loaded model"
                        : $"MAE {latestLinearTrainingResult.MeanAbsoluteError:0.0}, RMSE {latestLinearTrainingResult.RootMeanSquaredError:0.0}";
                    LinearEvaluatorSummaryTextBlock.Text =
                        $"Model: {latestLinearModel.Name}\n" +
                        $"Samples: {latestLinearModel.TrainingSampleCount}\n" +
                        $"{accuracy}\n" +
                        $"Use for AI: {(UseLinearEvaluatorCheckBox?.IsChecked == true ? "yes" : "no")}";
                }
            }

            SetLinearEvaluatorBusy(isLinearTrainingRunning);
        }

        //-----------------------------------------------------------------------
        private SearchEngine CreateActiveSearchEngine()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(CreateActiveSearchEngine);
            }

            bool useLinearEvaluator = UseLinearEvaluatorCheckBox?.IsChecked == true && latestLinearModel != null;
            return CreateActiveSearchEngine(useLinearEvaluator, latestLinearModel);
        }

        //-----------------------------------------------------------------------
        private SearchEngine CreateActiveSearchEngine(SearchConfiguration configuration)
        {
            return CreateActiveSearchEngine(configuration.UseLinearEvaluator, configuration.LinearModel);
        }

        //-----------------------------------------------------------------------
        private SearchEngine CreateActiveSearchEngine(bool useLinearEvaluator, LinearEvaluationModel? linearModel)
        {
            if (useLinearEvaluator && linearModel != null)
            {
                return new SearchEngine(moveGenerator, new LinearEvaluation(linearModel), new Random(1));
            }

            return searchEngine;
        }

        //-----------------------------------------------------------------------
        private static double ReadPositiveDouble(string? text, double defaultValue, double minimum, double maximum)
        {
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value))
            {
                value = defaultValue;
            }

            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        //-----------------------------------------------------------------------
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            isContinuousAnalysisEnabled = true;
            isBackgroundAnalysisPaused = false;
            if (AutoAnalyzeCheckBox != null)
            {
                AutoAnalyzeCheckBox.IsChecked = true;
            }
            UpdateAnalysisControlButtons();
            SaveApplicationSettingsSafe();
            StartBackgroundPositionAnalysis("Manual analysis");
        }

        private void PauseAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            isContinuousAnalysisEnabled = true;
            isBackgroundAnalysisPaused = true;
            CancelBackgroundPositionAnalysis();
            UpdateAnalysisControlButtons();
            SaveApplicationSettingsSafe();
            SetAnalysisSummary("Continuous analysis paused. Click Resume to continue from the current position.");
        }

        private void ResumeAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            isContinuousAnalysisEnabled = true;
            isBackgroundAnalysisPaused = false;
            if (AutoAnalyzeCheckBox != null)
            {
                AutoAnalyzeCheckBox.IsChecked = true;
            }
            UpdateAnalysisControlButtons();
            SaveApplicationSettingsSafe();
            StartBackgroundPositionAnalysis("Analysis resumed");
        }

        private void StopAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            isContinuousAnalysisEnabled = false;
            isBackgroundAnalysisPaused = false;
            if (AutoAnalyzeCheckBox != null)
            {
                AutoAnalyzeCheckBox.IsChecked = false;
            }
            CancelBackgroundPositionAnalysis();
            UpdateAnalysisControlButtons();
            SaveApplicationSettingsSafe();
            SetAnalysisSummary("Continuous analysis stopped. Click Analyze or enable Auto-analyze to restart.");
        }

        private void AutoAnalyzeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (isApplyingUserSettings || AutoAnalyzeCheckBox == null)
            {
                return;
            }

            isContinuousAnalysisEnabled = AutoAnalyzeCheckBox.IsChecked == true;
            if (!isContinuousAnalysisEnabled)
            {
                isBackgroundAnalysisPaused = false;
                CancelBackgroundPositionAnalysis();
                SetAnalysisSummary("Auto-analysis disabled. Click Analyze to run it manually.");
            }
            else
            {
                isBackgroundAnalysisPaused = false;
                StartBackgroundPositionAnalysis("Auto-analysis enabled");
            }

            UpdateAnalysisControlButtons();
            SaveApplicationSettingsSafe();
        }

        private void UpdateAnalysisControlButtons()
        {
            if (PauseAnalysisButton != null)
            {
                PauseAnalysisButton.IsEnabled = isContinuousAnalysisEnabled && !isBackgroundAnalysisPaused;
            }

            if (ResumeAnalysisButton != null)
            {
                ResumeAnalysisButton.IsEnabled = isContinuousAnalysisEnabled && isBackgroundAnalysisPaused;
            }

            if (StopAnalysisButton != null)
            {
                StopAnalysisButton.IsEnabled = isContinuousAnalysisEnabled || isBackgroundAnalysisPaused;
            }

            UpdateEngineStatusBar();
        }

        //-----------------------------------------------------------------------
        private SearchResult SearchBestMove(int[,] sourceBoard, int player, bool analysisOnly = false)
        {
            SearchConfiguration configuration = CaptureSearchConfiguration(player, analysisOnly);
            return SearchBestMove(sourceBoard, player, configuration);
        }

        //-----------------------------------------------------------------------
        private SearchResult SearchBestMove(int[,] sourceBoard, int player, SearchConfiguration configuration)
        {
            WinDama.Core.SearchOptions options = new WinDama.Core.SearchOptions
            {
                Mode = ToCoreSearchMode(configuration.Mode),
                MaximumDepth = Math.Max(1, configuration.MaximumDepth),
                TimeLimitMilliseconds = configuration.TimeLimitMilliseconds,
                Iterative = configuration.Iterative,
                TieBreakTolerance = AiTieBreakTolerance,
                RandomizeNearBestMoves = true,
                ForcedCaptureComparisonDepth = configuration.ForcedCaptureComparisonDepth,
                ProgressIntervalMilliseconds = 200,
                Progress = progress => UpdateAnalysisProgress(progress)
            };

            SearchEngine activeEngine = CreateActiveSearchEngine(configuration);
            SearchResult result = activeEngine.FindBestMove(sourceBoard, player, options);

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateAnalysisFields(result));
            }
            else
            {
                UpdateAnalysisFields(result);
            }

            return result;
        }

        //-----------------------------------------------------------------------
        private static WinDama.Core.SearchMode ToCoreSearchMode(AiSearchMode mode)
        {
            return mode switch
            {
                AiSearchMode.FixedTimePerMove => WinDama.Core.SearchMode.FixedTimePerMove,
                AiSearchMode.GameClock => WinDama.Core.SearchMode.GameClock,
                _ => WinDama.Core.SearchMode.FixedDepth
            };
        }

        //-----------------------------------------------------------------------
        //-----------------------------------------------------------------------
        //-----------------------------------------------------------------------
        // Legacy debug search/move wrappers moved to Core or removed.

        //-----------------------------------------------------------------------
        private bool HandleMoveExecution(Move move, int player)
        {
            CancelBackgroundPositionAnalysis();
            // Save the full UI state before changing anything.
            UndoStack.Push(CreateSnapshot());
            RedoStack.Clear();

            // The Core controller now owns validation, real move execution,
            // turn switching, and game-over detection.
            GameTurnResult result = gameController.ApplyMove(move);
            if (!result.MoveApplied)
            {
                SetStatus(result.Message);
                return false;
            }

            SynchronizeFieldsFromGameController();
            gameStateVersion++;
            UpdateBoardUI();
            UpdateCurrentPlayerLabel();
            moveSoundPlayer.Play();

            if (result.IsGameOver)
            {
                StopAllTimers();
                string message = string.IsNullOrWhiteSpace(result.Message)
                    ? gameController.GetStatusMessage()
                    : result.Message;
                SetAnalysisSummary($"Game over. {message}");
                MessageBox.Show(message, "Game Over");
            }
            else if (result.TurnSwitched)
            {
                StartTimerForCurrentPlayer();
                ScheduleAIMoveIfNeeded();
                StartBackgroundPositionAnalysis("Position changed");
            }
            else
            {
                SetStatus(result.Message);
            }

            return result.TurnSwitched;
        }

        //-----------------------------------------------------------------------
        private Move? CloneMove(Move? move)
        {
            if (move == null)
            {
                return null;
            }

            return new Move(move.Start, move.End, new List<(int, int)>(move.CapturedPieces));
        }

        //-----------------------------------------------------------------------
        private void HandleSquareClick(int row, int column)
        {
            if (isBoardEditorMode)
            {
                ApplyBoardEditorClick(row, column);
                return;
            }

            if (isGameOver)
            {
                SetStatus("The game is over. Start a new game or use Undo.");
                return;
            }

            if (isAiMoveInProgress || IsAiControlledPlayer(currentPlayer))
            {
                SetStatus(isAiMoveInProgress
                    ? "AI is thinking. Please wait for the move to finish."
                    : "This side is controlled by the AI in the selected game mode.");
                ScheduleAIMoveIfNeeded();
                return;
            }

            if (!BoardEditorController.IsInsideBoard(row, column))
            {
                ClearMoveSelection();
                return;
            }

            int clickedPiece = board[row, column];

            // Selecting or reselecting one of the current player's pieces.
            if (IsCurrentPlayerPiece(clickedPiece))
            {
                List<Move> legalMovesForPiece = GetLegalMovesForPiece(row, column);
                if (legalMovesForPiece.Count == 0)
                {
                    bool anotherPieceMustCapture = gameController.GetCurrentLegalMoves().Any(move => move.CapturedPieces.Count > 0);
                    ClearMoveSelection();
                    SetStatus(anotherPieceMustCapture
                        ? "Capture is mandatory. Select one of the pieces that has a highlighted capture."
                        : "This piece has no legal move. Select another piece.");
                    return;
                }

                SelectPiece(row, column, legalMovesForPiece);
                return;
            }

            // Empty target square after a piece was selected.
            if (pieceSelected && selectedRow.HasValue && selectedColumn.HasValue)
            {
                Move validMove = highlightedLegalMoves.FirstOrDefault(move => move.End.Item1 == row && move.End.Item2 == column);
                if (validMove != null)
                {
                    ClearMoveSelection(redraw: false);
                    bool turnSwitched = HandleMoveExecution(validMove, currentPlayer);

                    // The selected game mode decides whether the next side is human or AI.
                    if (turnSwitched)
                    {
                        ScheduleAIMoveIfNeeded();
                    }

                    return;
                }
            }

            if (clickedPiece != 0)
            {
                ClearMoveSelection();
                SetStatus("It is not this piece's turn.");
            }
            else if (pieceSelected)
            {
                ClearMoveSelection();
                SetStatus("Invalid target square. Select a highlighted square or choose another piece.");
            }
            else
            {
                ClearMoveSelection();
            }
        }

        //-----------------------------------------------------------------------
        private bool IsCurrentPlayerPiece(int piece)
        {
            return gameController.IsCurrentPlayerPiece(piece);
        }

        //-----------------------------------------------------------------------
        private List<Move> GetLegalMovesForPiece(int row, int column)
        {
            return gameController.GetLegalMovesForPiece(row, column);
        }

        //-----------------------------------------------------------------------
        private void SelectPiece(int row, int column, List<Move> legalMovesForPiece)
        {
            selectedRow = row;
            selectedColumn = column;
            pieceSelected = true;
            highlightedLegalMoves.Clear();
            highlightedLegalMoves.AddRange(legalMovesForPiece);

            UpdateBoardUI();

            int captureCount = legalMovesForPiece.Count(move => move.CapturedPieces.Count > 0);
            if (captureCount > 0)
            {
                SetStatus($"Selected piece ({row + 1}, {column + 1}). Capture required: {captureCount} highlighted capture move(s).");
            }
            else
            {
                SetStatus($"Selected piece ({row + 1}, {column + 1}). {legalMovesForPiece.Count} legal move(s) highlighted.");
            }
        }

        //-----------------------------------------------------------------------
        private void ClearMoveSelection(bool redraw = true)
        {
            pieceSelected = false;
            selectedRow = null;
            selectedColumn = null;
            highlightedLegalMoves.Clear();

            if (redraw)
            {
                UpdateBoardUI();
                UpdateCurrentPlayerLabel();
            }
        }

        //-----------------------------------------------------------------------
        // Legacy animation helpers were removed; board changes are rendered through BoardRenderer.

        //-----------------------------------------------------------------------
        // Legacy image-control factory methods were removed; BoardRenderer now owns piece rendering.

        //-----------------------------------------------------------------------
        private async void ScheduleAIMoveIfNeeded()
        {
            if (isBoardEditorMode || isAiMoveInProgress || !gameController.ShouldScheduleAiTurn())
            {
                return;
            }

            isAiMoveInProgress = true;
            long schedulerVersion = gameStateVersion;
            UpdateCurrentPlayerLabel();

            try
            {
                while (gameController.ShouldScheduleAiTurn())
                {
                    // Make forced captures visible to the user. The clock is stopped
                    // while the AI compares/plays mandatory captures, but the UI still
                    // pauses briefly so captures are not applied too abruptly.
                    if (CurrentSideHasMandatoryCapture())
                    {
                        await Task.Delay(ForcedCaptureDisplayDelayMilliseconds);
                    }
                    else
                    {
                        await Task.Delay(150);
                    }

                    if (schedulerVersion != gameStateVersion || isBoardEditorMode || isGameOver)
                    {
                        break;
                    }

                    bool moveWasPlayed = await PlayAIMoveForCurrentPlayerAsync();
                    if (!moveWasPlayed || !gameController.ShouldContinueAutomaticAiLoop())
                    {
                        break;
                    }

                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
            }
            finally
            {
                isAiMoveInProgress = false;
                UpdateCurrentPlayerLabel();
                StartBackgroundPositionAnalysis("Position analysis");
            }
        }


        //-----------------------------------------------------------------------
        private bool CurrentSideHasMandatoryCapture()
        {
            if (isGameOver)
            {
                return false;
            }

            List<Move> legalMoves = moveGenerator.GetPlayerCapturesOrMoves(board, currentPlayer);
            return legalMoves.Any(move => move.CapturedPieces != null && move.CapturedPieces.Count > 0);
        }

        //-----------------------------------------------------------------------
        private async Task<bool> PlayAIMoveForCurrentPlayerAsync()
        {
            CancelBackgroundPositionAnalysis();
            int searchPlayer = currentPlayer;
            long searchVersion = gameStateVersion;
            int[,] searchBoard = (int[,])board.Clone();

            List<Move> allMoves = moveGenerator.GetPlayerCapturesOrMoves(searchBoard, searchPlayer);
            if (allMoves.Count == 0)
            {
                // The controller owns the real game-over state; keep the UI in sync.
                gameController.RestoreState(board, currentPlayer, isGameOver: true, lastMove: lastMove);
                SynchronizeFieldsFromGameController();
                StopAllTimers();
                SetAnalysisSummary($"Game over. {GetPlayerLabel(searchPlayer)} has no valid moves.");
                MessageBox.Show($"{GetPlayerLabel(searchPlayer)} has no valid moves. {GetPlayerLabel(-searchPlayer)} wins!", "Game Over");
                return false;
            }

            if (TryGetOpeningBookMove(searchBoard, searchPlayer, allMoves, out Move? bookMove, out string bookReason) && bookMove != null)
            {
                StopAllTimers();
                SetAnalysisSummary($"Opening book move: {bookReason}");
                SetStatus($"{GetPlayerLabel(searchPlayer)} plays from opening book: {MoveNotation.Format(bookMove)}");

                if (isGameOver || currentPlayer != searchPlayer || gameStateVersion != searchVersion || !IsAiControlledPlayer(searchPlayer))
                {
                    SetAnalysisSummary("Opening book move discarded because the position or game mode changed.");
                    return false;
                }

                HandleMoveExecution(bookMove, searchPlayer);
                return true;
            }

            bool hasMandatoryCapture = allMoves.Any(move => move.CapturedPieces != null && move.CapturedPieces.Count > 0);
            if (hasMandatoryCapture)
            {
                // Do not consume the game clock for mandatory longest captures.
                // If several longest captures exist, the core search still compares
                // those capture alternatives tactically using ForcedCaptureComparisonDepth.
                StopAllTimers();
                SetAnalysisSummary($"{GetPlayerLabel(searchPlayer)} has a mandatory longest capture. Clock is paused while capture alternatives are compared.");
            }
            else
            {
                StartTimerForCurrentPlayer();
                SetAnalysisSummary($"{GetPlayerLabel(searchPlayer)} AI is searching...");
            }

            SearchConfiguration configuration = CaptureSearchConfiguration(searchPlayer);
            SearchResult result = hasMandatoryCapture
                ? SearchBestMove(searchBoard, searchPlayer, configuration)
                : await Task.Run(() => SearchBestMove(searchBoard, searchPlayer, configuration));
            UpdateAnalysisFields(result);

            if (isGameOver || currentPlayer != searchPlayer || gameStateVersion != searchVersion || !IsAiControlledPlayer(searchPlayer))
            {
                SetAnalysisSummary("AI search result discarded because the position or game mode changed.");
                return false;
            }

            if (result.BestMove == null)
            {
                gameController.RestoreState(board, currentPlayer, isGameOver: true, lastMove: lastMove);
                SynchronizeFieldsFromGameController();
                StopAllTimers();
                SetAnalysisSummary($"Game over. {GetPlayerLabel(searchPlayer)} AI has no valid moves.");
                MessageBox.Show($"{GetPlayerLabel(searchPlayer)} AI has no valid moves. {GetPlayerLabel(-searchPlayer)} wins!", "Game Over");
                return false;
            }

            HandleMoveExecution(result.BestMove, searchPlayer);
            return true;
        }

        //-----------------------------------------------------------------------
        private string GetPlayerLabel(int player)
        {
            return GameController.GetPlayerLabel(player);
        }

        //-----------------------------------------------------------------------
        // Legacy/debug move-generation, setup-position, and flip-board helpers were removed.
        // The WPF layer now delegates rule logic to WinDama.Core.MoveGenerator,
        // WinDama.Core.MoveExecutor, GameController, and SearchEngine.
        //-----------------------------------------------------------------------
    }
}
