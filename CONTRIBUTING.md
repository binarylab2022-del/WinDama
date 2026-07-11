# Contributing to WinDama

Thank you for considering a contribution.

## Development setup

Requirements:

- Windows 10/11
- Visual Studio 2022 or later
- .NET 6 SDK

Open:

```text
WinDama1.0/WinDama.sln
```

Run:

```text
Build > Rebuild Solution
Test > Run All Tests
```

The current expected baseline is:

```text
101 passed, 0 failed
```

## Contribution priorities

Good first contributions:

- add tactical test positions;
- improve README/user documentation;
- improve board/editor UI clarity;
- add game-history/move-list navigation;
- add more opening-book lines.

Engine contributions:

- strengthen search ordering;
- improve quiescence and endgame behavior;
- tune evaluation profiles;
- improve tournament/benchmark tools;
- improve dataset export for ML research.

## Pull request checklist

Before submitting a pull request:

1. Rebuild the solution.
2. Run all tests.
3. Add or update tests for rule/search changes.
4. Keep `WinDama.Core` independent of WPF.
5. Avoid adding generated binaries or local build output.
6. Update `RELEASE_NOTES.md` when adding significant features.

## Coding guidelines

- Keep rules and search logic in `WinDama.Core`.
- Keep WPF classes focused on UI rendering and interaction.
- Avoid duplicate rule logic in the UI.
- Prefer deterministic tests.
- Add saved tactical positions when fixing engine regressions.
