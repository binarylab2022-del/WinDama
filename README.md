# WinDama — Algerian / Spanish Checkers

[![Build](https://github.com/binarylab2022-del/WinDama/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/binarylab2022-del/WinDama/actions/workflows/build.yml)
[![Tests](https://github.com/binarylab2022-del/WinDama/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/binarylab2022-del/WinDama/actions/workflows/tests.yml)
![Test baseline](https://img.shields.io/badge/tests-101%20passed-brightgreen)
[![Release](https://img.shields.io/github/v/release/binarylab2022-del/WinDama?include_prereleases)](https://github.com/binarylab2022-del/WinDama/releases)
[![License](https://img.shields.io/github/license/binarylab2022-del/WinDama)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)
![Framework](https://img.shields.io/badge/.NET-6.0-purple)

WinDama is an open-source WPF desktop application and engine-development
environment for Algerian / Spanish checkers. It combines a playable graphical
interface, a separated rules engine, AI search, tactical benchmarking,
evaluation tuning, opening-book support, tournament tools, and dataset export.

<p align="center">
  <img src="docs/screenshots/main-board.png"
       alt="WinDama main board and analysis interface"
       width="900">
</p>

## Highlights

- Mandatory capture and longest multi-capture enforcement
- Flying Dama movement and capture rules
- Human vs AI, Human vs Human, and AI vs AI
- Alpha-beta search with iterative deepening
- Transposition tables, quiescence search, killer and history heuristics
- Real-time engine analysis and principal variation
- Tactical benchmark and evaluation tuner
- Profile-vs-profile tournament runner
- Opening-book and game-database tools
- Dataset export for future learned evaluators
- 101 passing NUnit tests
- Bitboard and FPGA-oriented research tracks

## Screenshots

### Mandatory capture and live analysis

<p align="center">
  <img src="docs/screenshots/capture-analysis.png"
       alt="Mandatory capture highlighting and continuous engine analysis"
       width="850">
</p>

### Evaluation tuning

<p align="center">
  <img src="docs/screenshots/evaluation-tuner.png"
       alt="WinDama evaluation weight tuner"
       width="850">
</p>

### AI profile tournament

<p align="center">
  <img src="docs/screenshots/tournament.png"
       alt="WinDama profile tournament runner"
       width="850">
</p>

### Board editor and tactical test positions

<p align="center">
  <img src="docs/screenshots/board-editor.png"
       alt="WinDama board editor for tactical positions"
       width="850">
</p>

## Download

The current Windows x64 preview release is available from the
[GitHub Releases page](https://github.com/binarylab2022-del/WinDama/releases).

1. Download the Windows x64 ZIP archive.
2. Extract all files into the same folder.
3. Run `WinDama.exe`.

The self-contained package normally does not require a separate .NET
installation.

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


### Bitboard implementation research track

The current stable engine uses readable Core classes (`MoveGenerator`, `MoveExecutor`, `GameController`) as the reference implementation. A parallel research/development track is planned for a high-performance bitboard implementation of the same Spanish-checkers rules.

#### Board representation

The bitboard model uses the 32 playable dark squares of the 8x8 board. Each position can be represented by four 32-bit masks:

```text
P1Men    : bits occupied by Player 1 ordinary men
P1Damas  : bits occupied by Player 1 Damas / kings
P2Men    : bits occupied by Player 2 ordinary men
P2Damas  : bits occupied by Player 2 Damas / kings
```

Derived masks:

```text
P1Pieces = P1Men | P1Damas
P2Pieces = P2Men | P2Damas
Occupied = P1Pieces | P2Pieces
Empty    = ~Occupied & PlayableMask32
```

The implementation should keep a deterministic mapping between board coordinates and playable-square indices:

```text
(row, column) <-> playable index 0..31
```

This makes it possible to convert between the WPF board, JSON test positions, opening-book moves, and the compact bitboard representation.

#### Simple man moves

Ordinary non-capturing man moves can be generated with directional shifts and edge masks. For each side, only forward diagonals are legal for quiet moves. A typical bitboard move-generation step is:

```text
candidateTargets = ShiftForwardLeft(myMen) | ShiftForwardRight(myMen)
legalTargets     = candidateTargets & Empty
```

Edge masks are required so pieces do not wrap from one side of the board to the other after a shift.

#### Dama / king quiet moves

Spanish-checkers Damas are flying pieces. Their quiet moves can be generated with precomputed diagonal rays:

```text
Ray[square][direction] -> bit mask of all squares along that diagonal direction
```

For each Dama and each diagonal direction:

1. read the ray mask;
2. find the first occupied square on the ray;
3. all empty squares before that blocker are legal quiet destinations;
4. if there is no blocker, all ray squares are legal quiet destinations.

This can be implemented either with bit scans or with precomputed blocker-to-move lookup tables.

#### Simple captures

For ordinary men, a capture exists when an adjacent diagonal square contains an opponent piece and the square immediately beyond it is empty:

```text
opponentAdjacent = ShiftDiagonal(myMen) & OpponentPieces
landingSquare    = ShiftSameDirection(opponentAdjacent) & Empty
```

The current rule engine requires mandatory capture, so if any capture exists, quiet moves must be discarded.

#### Dama captures

For a flying Dama, a capture ray is valid when the ray contains an opponent piece as the first relevant capturable piece and at least one empty landing square beyond it. The generator must:

1. scan the ray from the Dama square;
2. locate the first occupied square;
3. require that this blocker is an opponent piece;
4. collect all empty landing squares beyond the captured piece until the next blocker.

The move record must store both the landing square and the captured square.

#### Multi-captures and longest-capture rule

Spanish checkers requires the longest capture sequence. The bitboard engine should therefore generate captures with a DFS/backtracking procedure:

```text
GenerateCaptures(position, pieceSquare, capturedMask, path)
```

At each recursive step:

1. generate all legal captures for the current piece;
2. remove the captured piece from the opponent bitboard;
3. move the capturing piece to the landing square;
4. mark the captured square in `capturedMask` so the same piece cannot be captured twice;
5. continue until no further capture is available.

After all sequences are generated, keep only those with maximum captured-piece count:

```text
maxLen = max(sequence.CapturedCount)
legalCaptures = sequences where CapturedCount == maxLen
```

Promotion during a capture must follow the current engine rule: when an ordinary man reaches the promotion row during a capture, it promotes and the capture sequence stops; the newly promoted Dama does not continue capturing in the same turn.

#### Suggested bitboard API

A future `WinDama.Core.Bitboards` namespace can expose:

```text
BitboardPosition
BitboardMove
BitboardMoveGenerator
BitboardMoveExecutor
BitboardSearchAdapter
BitboardPerft
```

The first goal should be functional equivalence with the existing reference engine. Every bitboard move generator result should be compared against `MoveGenerator` using the existing JSON tactical positions.

### Boolean / FPGA implementation research track

A second research direction is a Boolean and table-driven representation of the move generator, suitable for FPGA acceleration. This is related to the idea of expressing rule legality as Boolean conditions over fixed-width board vectors.

#### Boolean board model

The 32 playable squares can be represented as Boolean vectors:

```text
p1_man[32]
p1_dama[32]
p2_man[32]
p2_dama[32]
empty[32]
```

Move legality can then be expressed using Boolean equations or lookup tables over relevant local masks.

#### Simple moves as Boolean equations

For ordinary men, each possible source/target pair can be encoded as a fixed rule:

```text
legal_simple_move_i_j = my_man[i] AND empty[j] AND direction_ok(i, j)
```

Only legal diagonal neighbor pairs need to be encoded.

#### Capture moves as Boolean equations

For ordinary men:

```text
legal_capture_i_k_j = my_man[i] AND opponent[k] AND empty[j]
```

where `i` is the source, `k` is the jumped square, and `j` is the landing square.

For Damas, each candidate capture can be represented using:

```text
source square
captured square
landing square
path mask before captured piece
path mask after captured piece
```

A Dama capture is legal when:

```text
my_dama[source]
AND opponent[captured]
AND empty[landing]
AND no blocker on path(source, captured)
AND no blocker on path(captured, landing)
```

The path masks can be stored in ROM tables.

#### Table-driven FPGA architecture

A practical FPGA architecture can use precomputed tables:

```text
MoveTable       : source -> candidate quiet moves
CaptureTable    : source -> candidate captures
PathMaskTable   : source/captured/landing -> blocker masks
RayTable        : Dama diagonal rays
PromotionTable  : landing square -> promotes?
```

The hardware pipeline can evaluate many candidate moves in parallel by applying Boolean tests to the board vectors.

#### Multi-capture FPGA approach

Multi-capture generation can be implemented with either:

1. a small finite-state machine that performs DFS over capture states;
2. a microcoded capture explorer;
3. a bounded-depth parallel expansion network;
4. a hybrid CPU/FPGA approach where FPGA generates capture successors and CPU controls DFS.

Each capture state stores:

```text
current square
piece type
P1/P2 bitboards
capturedMask
capture count
path encoding
```

The FPGA can also compute the maximum capture length by comparing all generated capture paths and returning only maximal sequences.

#### Verification strategy

The Boolean/FPGA move generator must be verified against the reference Core engine:

```text
1. Load each JSON tactical position.
2. Generate legal moves with the C# reference MoveGenerator.
3. Generate legal moves with the Boolean/bitboard model.
4. Compare move sets exactly.
5. Repeat for random positions and AI-vs-AI generated positions.
```

Useful verification targets:

```text
mandatory capture
longest multi-capture
Dama flying captures
promotion-capture stop
no recapturing the same piece
blocked/no-move positions
```

This creates a path from a readable C# reference engine to a compact bitboard engine and then to a Boolean/table-driven FPGA accelerator.

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
dotnet restore WinDama.sln
dotnet build WinDama.sln -c Release
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
7. Implement `WinDama.Core.Bitboards` as a high-performance equivalent of the reference move generator.
8. Add `BitboardPerft` and exact move-set comparison tests against the current `MoveGenerator`.
9. Develop the Boolean/table-driven move-generation model for FPGA-oriented research.
10. Prototype an FPGA accelerator for legal move generation and maximal multi-capture search.
