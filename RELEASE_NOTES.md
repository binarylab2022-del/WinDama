# Release Notes

## Platform migration to .NET 10

- Retargeted the reusable Core engine and NUnit tests to `net10.0`.
- Retargeted the WPF application to `net10.0-windows`.
- Updated GitHub Actions to install the .NET 10 SDK.
- Updated the README framework badge and development requirements.
- Preserved the Windows x64 publishing target and existing validation suite.

Repository: `https://github.com/binarylab2022-del/WinDama`

## WinDama 1.0.0-preview

Initial open-source preview release of **WinDama**, a WPF desktop application and engine-development environment for Algerian / Spanish checkers.

This version is prepared as the GitHub continuation of the earlier SourceForge project **Algerian Spanish Checkers**.

### Validation baseline

- NUnit tests: **101 passed, 0 failed**
- Target framework: **.NET 10.0 Windows / WPF**
- Release runtime target: **win-x64**

### Main engine features

- Centralized rule engine in `WinDama.Core`.
- Mandatory capture and longest multi-capture enforcement.
- Flying Dama movement and capture rules.
- Promotion handling, including stopping a capture sequence when a man promotes.
- Alpha-beta search with iterative deepening.
- Fixed-depth, fixed-time, and game-clock AI modes.
- Principal variation and top-5 candidate move comparison.
- Transposition table with Zobrist hashing.
- Quiescence / capture-extension search.
- Killer-move and history heuristics.
- Bundled `.ouv` opening-book import with mirrored orientation support.

### Analysis and research tools

- Continuous background analysis with Pause / Resume / Stop controls.
- Board-position editor.
- Tactical position JSON format.
- Tactical benchmark scoring and ranking.
- Evaluation profile support.
- Automatic evaluation-weight tuner.
- Profile-vs-profile AI tournament runner.
- Opening-book/game-database tools.
- Position dataset exporter for future ML/NNUE work.
- Linear learned evaluator support.

### WPF application features

- Human vs AI, Human vs Human, and AI vs AI modes.
- Legal-move and last-move highlighting.
- Responsive board with row/column notation.
- Persistent settings in `%APPDATA%\WinDama\settings.json`.
- Engine/status bar.
- About / Version panel.
- Benchmark, tuner, tournament, opening-book, and dataset panels.

### Open-source release notes

This preview is intended for:

- testing the engine rules;
- improving AI search/evaluation;
- expanding tactical and endgame test positions;
- preparing data for future machine-learning and NNUE-style evaluation;
- community review and contributions.

### Known limitations

- The WPF layer is much cleaner than the original prototype, but `MainWindow.xaml.cs` can still be further reduced.
- The handcrafted and learned evaluators need more benchmark-driven tuning.
- The bundled opening book is useful but should be expanded and validated against more historical games.
- The linear learned evaluator is a first ML step, not a full NNUE implementation.

### Recommended next development steps

1. Add searchable game history and move-list navigation.
2. Expand tactical/endgame benchmark positions.
3. Add PGN-like import/export for Spanish checkers games.
4. Improve tournament scheduling and parallel execution.
5. Add stronger dataset labeling from deeper searches.
6. Experiment with small neural evaluators, then NNUE-style evaluation.
