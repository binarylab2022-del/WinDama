# WinDama ├تظéشظإ Algerian / Spanish Checkers

[![Build](https://github.com/binarylab2022-del/WinDama/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/binarylab2022-del/WinDama/actions/workflows/build.yml) [![Tests](https://github.com/binarylab2022-del/WinDama/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/binarylab2022-del/WinDama/actions/workflows/tests.yml) ![Test baseline](https://img.shields.io/badge/tests-101%20passed-brightgreen) [![Release](https://img.shields.io/github/v/release/binarylab2022-del/WinDama?include_prereleases)](https://github.com/binarylab2022-del/WinDama/releases) [![License](https://img.shields.io/github/license/binarylab2022-del/WinDama)](LICENSE) ![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue) ![Framework](https://img.shields.io/badge/.NET-10.0-purple)

WinDama is an open-source WPF application and reusable C# engine for
Algerian / Spanish checkers. It combines a playable interface, centralized
rules, AI search, benchmarking, opening books, tournaments, dataset export,
and research on bitboards and FPGA acceleration.

![WinDama main board and analysis interface](docs/screenshots/main-board.png)

## Why WinDama?

WinDama is designed as:

- a complete playable checkers application;
- an educational board-game AI platform;
- a reusable C# rules and search engine;
- a benchmark environment for search and evaluation;
- a research platform for bitboards and FPGA move generation.

## Highlights

- Mandatory capture, longest multicaptures, and flying Damas
- Human vs AI, Human vs Human, and AI vs AI
- Iterative-deepening alpha-beta and real-time analysis
- Transposition tables, quiescence, killer, and history heuristics
- Benchmarks, evaluator tuning, profile tournaments, and datasets
- Opening books, game databases, and 101 passing NUnit tests

## Screenshots

![Mandatory capture and live analysis](docs/screenshots/capture-analysis.png)

![AI profile tournament runner](docs/screenshots/tournament.png)

See the [complete screenshot gallery](docs/SCREENSHOTS.md).

## Download

Download the Windows x64 preview from
[GitHub Releases](https://github.com/binarylab2022-del/WinDama/releases),
extract the ZIP, and run `WinDama.exe`.

The self-contained package normally requires no separate .NET installation.

## Current baseline

- Version: `1.0.0-preview`
- Engine: `Core engine 2026.07`
- Validation: `101 passed, 0 failed`
- Framework: `.NET 10.0 Windows / WPF`
- License: [MIT](LICENSE)

## Engine architecture

WinDama separates the WPF interface from the reusable engine.

```text
WPF application
      |
      v
WinDama.Core
      |
      +-- Move generation and rules
      +-- Search and evaluation
      +-- Opening books and datasets
      |
      v
WinDama.Tests
```

```text
MainWindow
    |
    v
GameController
    |
    +-- MoveGenerator
    +-- MoveExecutor
    +-- SearchEngine
    +-- Evaluation
    +-- OpeningBook
```

Further documentation:

- [Engine Architecture](docs/ENGINE_ARCHITECTURE.md)
- [Move Generation and Rules](docs/MOVE_GENERATION.md)
- [AI Search and Evaluation](docs/AI_SEARCH.md)
- [Bitboard Design](docs/BITBOARD_DESIGN.md)
- [FPGA Research](docs/FPGA_RESEARCH.md)

## Main capabilities

### Game and interface

- Responsive 8x8 board, notation, and legal-move highlighting
- Last-move highlighting, undo/redo, editor, and analysis controls
- Persistent settings and engine-status display

### Rules, AI, and tools

- Mandatory and maximal captures with flying-Dama rules
- Fixed-depth, timed, and game-clock search
- Opening books and configurable handcrafted or linear evaluators
- Tactical benchmarks, tournaments, and JSONL/CSV/JSON export

## Project structure

```text
WinDama1.0/        WPF interface
WinDama.Core/      Rules, search, and reusable tools
WinDama.Tests/     NUnit validation suite
OpeningBooks/      Bundled opening books
TestPositions/     Tactical JSON positions
EvaluationWeights/ Evaluation profiles
docs/              Technical documentation
scripts/           Build and release scripts
```

## Build and test

Requirements: Visual Studio Community 2026 with the .NET desktop development workload, or the .NET 10 SDK for command-line builds.

```powershell
dotnet restore WinDama.sln
dotnet build WinDama.sln -c Release
dotnet test .\WinDama.Tests\WinDama.Tests.csproj -c Release
```

Expected result: `101 passed, 0 failed`.

Publish Windows x64:

```powershell
.\scripts\Publish-Release-x64.ps1
```

Use `-SelfContained` to create a self-contained package.

## Research directions and publications

WinDama supports work on rule-based move generation, bitboards, maximal
multicaptures, Boolean legality models, FPGA acceleration, evaluation learning,
and tactical benchmarking.

Publications, preprints, datasets, and reproducible experiments will be listed
here when publicly available.

## Roadmap

- Extend tactical and endgame tests
- Add searchable game history and portable notation
- Implement and validate `WinDama.Core.Bitboards`
- Add exact move-set and perft comparisons
- Prototype Boolean and FPGA-oriented move generation
- Continue evaluation-learning experiments

## Open-source project

This repository continues the earlier
[Algerian Spanish Checkers project on SourceForge](https://sourceforge.net/projects/algerian-spanish-checkers/).

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) before
opening a pull request. See [SECURITY.md](SECURITY.md),
[RELEASE_NOTES.md](RELEASE_NOTES.md), and
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
