# WinDama - Algerian / Spanish Checkers

WinDama is a WPF desktop application and engine-development environment for Algerian / Spanish checkers. It combines a playable GUI, a separated core rules engine, AI search, tactical benchmarks, evaluation tuning, opening-book support, and dataset export tools for future machine-learning work.

## Open-source project status

This repository is prepared as the GitHub open-source continuation of the earlier SourceForge project **Algerian Spanish Checkers**:

```text
https://sourceforge.net/projects/algerian-spanish-checkers/
```

GitHub repository:

```text
https://github.com/binarylab2022-del/WinDama
```

The source code is released under the MIT License. See `LICENSE` for details.

For contributors, see `CONTRIBUTING.md`.
For release details, see `RELEASE_NOTES.md`.


## Current release baseline

- Version: `1.0.0-preview`
- Engine: `Core engine 2026.07`
- Validation baseline: `101 passing NUnit tests`
- Target framework: `.NET 6.0 Windows / WPF`

## Main features

### Game and UI

- Human vs AI, Human vs Human, and AI vs AI modes.
- Responsive 8x8 board with row/column notation.
- Legal-move highlighting directly on the board.
- Last-move highlighting.
- Full undo/redo using game snapshots.
- Pause, resume, and stop controls for continuous background analysis.
- Persistent settings in `%APPDATA%\WinDama\settings.json`.
- Engine/status bar showing active mode, evaluator, book state, search mode, and analysis state.
- About / Version panel.

### Rules engine

- Mandatory capture enforcement.
- Longest multi-capture enforcement.
- Flying Dama movement and capture support.
- Promotion handling, including stopping capture continuation after a man promotes.
- Centralized rule implementation in `WinDama.Core`.

### AI search

- Alpha-beta search.
- Iterative deepening.
- Fixed-depth, fixed-time, and game-clock search modes.
- Principal variation display.
- Top-5 candidate move comparison.
- Transposition table with Zobrist hashing.
- Quiescence / capture-extension search.
- Killer-move and history heuristics.
- Opening-book move selection in early plies.

### Opening book and game database

- Bundled `.ouv` opening book files.
- Automatic book loading at startup.
- Mirrored orientation support so legacy book lines work with the app's Player 1 starting side.
- Import `.ouv` files manually.
- Save/load game databases.
- Export opening-book statistics to CSV.

### Evaluation and benchmarking

- Handcrafted evaluator with configurable weights.
- Multiple evaluation profiles.
- Tactical benchmark runner with automatic scoring/ranking.
- Evaluation-weight tuner.
- Profile-vs-profile AI tournament runner.
- CSV and game-log export.
- Linear learned evaluator support.
- Learned evaluator can be included in tactical benchmarks and tournaments.

### Dataset / ML preparation

- Position dataset exporter.
- JSONL, CSV, and JSON export formats.
- Samples include board state, side to move, evaluation, best move, principal variation, top moves, material counts, game result, profile, and search metadata.
- Intended as preparation for linear models, small neural evaluators, and later NNUE-style evaluation.

## Project structure

```text
WinDama1.0/
  MainWindow.xaml
  MainWindow.xaml.cs
  BoardRenderer.cs
  BoardEditorController.cs
  AnalysisPanelUpdater.cs
  ClockController.cs
  WinDama.csproj

WinDama.Core/
  MoveGenerator.cs
  MoveExecutor.cs
  GameController.cs
  SearchEngine.cs
  Evaluation.cs
  EvaluationWeights.cs
  TranspositionTable.cs
  OpeningBook.cs
  GameDatabase.cs
  TacticalBenchmarkRunner.cs
  EvaluationTournamentRunner.cs
  PositionDatasetExporter.cs
  LinearEvaluation*.cs

WinDama.Tests/
  NUnit rule, search, benchmark, tournament, opening-book, dataset, and evaluator tests

TestPositions/
  Tactical JSON positions used by tests and benchmark tools

EvaluationWeights/
  Evaluation profile JSON files

OpeningBooks/
  Bundled .ouv opening-book files
```

## Build and test

Open the solution:

```text
WinDama1.0/WinDama.sln
```

Then run:

```text
Build > Rebuild Solution
Test > Run All Tests
```

Expected baseline for this release:

```text
101 passed, 0 failed
```
### Command-line build

From the repository root:

```powershell
dotnet restore .\WinDama1.0\WinDama.sln
dotnet build .\WinDama1.0\WinDama.sln -c Release
dotnet test .\WinDama.Tests\WinDama.Tests.csproj -c Release
```

### Publish Windows x64 release

```powershell
.\scripts\Publish-Release-x64.ps1
```

For a self-contained package:

```powershell
.\scripts\Publish-Release-x64.ps1 -SelfContained
```

The release package is created under:

```text
artifacts/packages/
```



## Upload to GitHub

From the repository root, run:

```powershell
git init
git add .
git commit -m "Initial open-source WinDama release"
git branch -M main
git remote add origin https://github.com/binarylab2022-del/WinDama.git
git push -u origin main
```

If the remote repository already contains files, pull first:

```powershell
git pull origin main --allow-unrelated-histories
git push -u origin main
```

## Basic usage

1. Start the app.
2. Choose a game mode: Human vs AI, Human vs Human, or AI vs AI.
3. Choose AI search mode: fixed depth, fixed time per move, or game clock.
4. Move pieces by clicking a piece and then a highlighted destination.
5. Use the analysis panel to inspect depth, nodes, evaluation, best move, principal variation, transposition-table statistics, and top candidate moves.
6. Use Pause / Resume / Stop to control continuous analysis.
7. Use the board editor to create tactical positions.
8. Use benchmark/tuner/tournament panels to evaluate engine profiles.

## Release notes

This release adds release-polish features:

- live engine/status bar;
- About / Version panel;
- release README;
- persistent status visibility for evaluator, opening book, search mode, and analysis mode.

## Recommended next development steps

1. Add a searchable game-history panel.
2. Add PGN-like export/import for played games.
3. Improve tournament scheduling and parallel execution.
4. Add more tactical and endgame positions.
5. Add deeper automatic evaluation tuning.
6. Begin a small neural evaluator after enough datasets are collected.
