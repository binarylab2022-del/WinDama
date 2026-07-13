# WinDama Engine Architecture

## 1. Purpose

This document describes the organization of WinDama and the interaction
between the WPF application, the reusable Core engine, and the NUnit test
project.

The architecture has two primary goals:

1. keep user-interface code separate from game rules and artificial
   intelligence;
2. make the rules engine reusable for tests, command-line tools, tournaments,
   datasets, and future hardware-oriented implementations.

## 2. Solution organization

```text
WinDama.sln
|
+-- WinDama1.0/
|   +-- WPF views and controls
|   +-- board rendering
|   +-- user interaction
|   +-- analysis and status presentation
|
+-- WinDama.Core/
|   +-- game state
|   +-- move generation
|   +-- move execution
|   +-- search
|   +-- evaluation
|   +-- opening books
|   +-- benchmarks and tournaments
|   +-- dataset export
|
+-- WinDama.Tests/
    +-- rule tests
    +-- search tests
    +-- opening-book tests
    +-- benchmark and tournament tests
    +-- dataset and evaluator tests
```

Supporting directories contain test positions, opening books, evaluation
profiles, documentation, and release scripts.

## 3. Architectural layers

### 3.1 WPF presentation layer

The `WinDama1.0` project owns desktop-specific concerns:

- creating and arranging controls;
- drawing the board and pieces;
- mapping mouse input to board coordinates;
- displaying legal destinations and the previous move;
- presenting clocks, engine status, and analysis;
- opening configuration, benchmark, editor, and tournament panels;
- persisting user-facing settings.

This layer should not implement checkers rules directly. It asks the Core
engine for legal actions and displays the resulting state.

### 3.2 Core engine layer

`WinDama.Core` owns the reusable domain logic:

- representation of a position and side to move;
- legal move generation;
- mandatory-capture and maximal-capture filtering;
- execution and reversal of moves;
- game-state transitions;
- search and evaluation;
- opening books and game databases;
- benchmarking, tournaments, and dataset generation.

The Core project must remain independent of WPF controls and the Windows
dispatcher.

### 3.3 Test layer

`WinDama.Tests` validates Core behaviour without requiring the graphical
interface.

Tests should be deterministic whenever possible. Randomized tests should use a
known seed or persist the failing position so the result can be reproduced.

## 4. Main components

```text
MainWindow
    |
    +-- BoardRenderer
    +-- BoardEditorController
    +-- AnalysisPanelUpdater
    +-- ClockController
    |
    v
GameController
    |
    +-- GameState
    +-- MoveGenerator
    +-- MoveExecutor
    +-- SearchEngine
    |     +-- Evaluation
    |     +-- TranspositionTable
    |
    +-- OpeningBook
    +-- GameDatabase
    +-- TacticalBenchmarkRunner
    +-- EvaluationTournamentRunner
    +-- PositionDatasetExporter
```

### MainWindow

`MainWindow` coordinates the visible application. Its responsibility is
orchestration rather than rule implementation.

It receives UI events, requests an operation from the appropriate controller,
and refreshes the visible state.

### BoardRenderer

`BoardRenderer` translates the current game position into WPF visual elements.
It should draw:

- squares and coordinates;
- ordinary pieces and Damas;
- selected pieces;
- legal destinations;
- the previous move;
- editor-specific overlays.

Rendering should be a one-way operation from state to visuals.

### BoardEditorController

The editor controller manages placement and removal of pieces when creating
custom positions. A position produced by the editor must still be validated by
the Core engine before it is used for play or benchmarking.

### AnalysisPanelUpdater

This component presents search information such as:

- completed depth;
- elapsed time;
- node count;
- evaluation;
- best move;
- principal variation;
- transposition-table statistics;
- candidate move ranking.

It converts engine data into UI text and must marshal updates to the WPF
dispatcher when analysis runs on a background thread.

### ClockController

`ClockController` manages visible game clocks and translates the selected time
control into search budgets. It does not decide legal moves.

### GameController

`GameController` is the main application-facing Core coordinator. It is
responsible for:

- holding the current game state;
- applying legal human or AI moves;
- switching the side to move;
- maintaining snapshots for undo and redo;
- detecting terminal positions;
- coordinating opening-book and search requests.

### MoveGenerator

`MoveGenerator` returns all legal moves for a position.

Its result already reflects mandatory-capture and longest-capture rules, so UI
and search callers should not reimplement those filters.

### MoveExecutor

`MoveExecutor` applies a move to a position. Centralizing execution prevents
the user interface, search engine, and tests from using inconsistent board
updates.

### SearchEngine

`SearchEngine` selects a move using opening-book information and tree search.
It depends on legal move generation, move execution, evaluation, time
management, and transposition-table storage.

### Evaluation

The evaluator converts a nonterminal position into a score from a defined
player perspective. Evaluation profiles allow weights to be changed without
rewriting search logic.

### OpeningBook and GameDatabase

Opening books provide known early moves. Game databases support saved lines,
statistics, import, and export.

### Benchmark, tournament, and dataset components

These services reuse the same Core engine:

- `TacticalBenchmarkRunner` evaluates profiles on known positions;
- `EvaluationTournamentRunner` compares profiles through AI games;
- `PositionDatasetExporter` records positions and search labels for later
  analysis or model training.

## 5. Human move interaction

```text
User selects a square
        |
        v
MainWindow asks GameController for legal moves
        |
        v
MoveGenerator returns the complete legal set
        |
        v
BoardRenderer highlights valid destinations
        |
        v
User selects a destination
        |
        v
GameController validates and calls MoveExecutor
        |
        v
State, history, clocks, and UI are updated
```

The WPF layer never decides whether a move is legal by itself.

## 6. AI move interaction

```text
GameController starts an AI turn
        |
        +-- query OpeningBook
        |       |
        |       +-- book move found -> validate and play
        |
        +-- otherwise start SearchEngine
                |
                +-- iterative deepening
                +-- MoveGenerator
                +-- MoveExecutor
                +-- Evaluation
                +-- TranspositionTable
                |
                v
          best legal move
                |
                v
        GameController applies the move
```

Continuous analysis can use the same search components while keeping the
actual game state unchanged until a move is committed.

## 7. Snapshot and undo/redo model

A snapshot should contain all information required to restore the game
consistently:

- board position;
- side to move;
- game mode;
- clocks;
- previous move;
- result or terminal state;
- relevant analysis and UI state.

Undo and redo should restore snapshots rather than attempt to reconstruct
complex multicaptures from partial information.

## 8. Dependency rules

The intended dependency direction is:

```text
WinDama1.0  --->  WinDama.Core
WinDama.Tests ---> WinDama.Core
WinDama.Core -X->  WinDama1.0
```

Core classes must not reference:

- `Window`, `Button`, `Canvas`, or other WPF types;
- message boxes;
- the WPF dispatcher;
- application-specific controls.

Instead, Core services return data, status objects, or events that the UI can
present.

## 9. Extension points

The architecture supports future additions without replacing the UI:

- `WinDama.Core.Bitboards` can implement a second move generator;
- a command-line runner can use `WinDama.Core`;
- an FPGA verifier can compare hardware results with Core move sets;
- new evaluators can implement the same evaluation contract;
- new opening-book formats can be adapted behind the book interface;
- tournament and dataset tools can run without rendering the board.

## 10. Contributor guidance

When adding a feature:

1. place domain rules in `WinDama.Core`;
2. add deterministic tests in `WinDama.Tests`;
3. expose only the required data to the WPF layer;
4. keep background search cancellation explicit;
5. avoid duplicated move execution or turn switching;
6. document new public components and file formats.

This separation is essential because the readable Core engine is also the
reference model for future bitboard and FPGA implementations.
