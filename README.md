# Chain Chapters

Chain Chapters is a .NET 10 desktop puzzle game built with Avalonia.
The player rotates joints in a segmented chain until the chain fully covers a
target silhouette on a hex grid. Each shipped chapter uses the same chain
length, has exactly one full-cover solution, and starts from a sparse,
non-solved state.

## What Is In The Game

- 10 handcrafted chapters with solid, hole-free target silhouettes
- exact per-chapter par counts
- chapter gallery with silhouette previews and medal tracking
- drag controls, keyboard controls, undo/redo, and text-only or highlighted nudges
- local save data for exact in-progress boards, undo/redo history, chapter progress, and best runs
- local settings for animation speed, hint highlighting, and feedback beeps

## Runtime Architecture

The solution is split into a small number of focused layers:

- `src/ChainPuzzle.Core`
  - pure puzzle logic
  - chain geometry, move rules, solver, target-cover counting, chapter data, and validation
- `src/ChainPuzzle.Desktop`
  - desktop shell
  - `MainWindow` handles rendering, input, and animation timing
  - `GameViewModel` owns user-facing state, progress, medals, and settings
  - `ChainBoardControl` renders the board and live chain
  - `ShapePreviewControl` renders chapter silhouettes in the gallery
- `tests/ChainPuzzle.Tests`
  - gameplay, geometry, solver, desktop-state, and chapter-structure tests

More detail is in [docs/development.md](docs/development.md).

## Solution Layout

- `Solution2.sln` — root solution file
- `global.json` — pinned .NET SDK for reproducible local and CI builds
- `Directory.Build.props` — shared language/analyzer settings
- `.editorconfig` — formatting and style defaults
- `.github/workflows/ci.yml` — build-and-test workflow for push and pull request validation

## Run The Desktop App

```bash
dotnet run --project src/ChainPuzzle.Desktop/ChainPuzzle.Desktop.csproj
```

## Controls

- Mouse: click a joint and drag to rotate
- Keyboard: `Up` / `Down` selects a joint, `A` / `D` rotates
- Undo/redo: `Ctrl+Z`, `Ctrl+Y`, or `Ctrl+Shift+Z`
- Menu: `Esc`
- New run from menu: `N`

## Save Data

The desktop app stores local data under the user application data folder in a
`ChainPuzzle` directory:

- `progress.json` — current chapter, current board, move count, undo/redo history, cleared chapters, and best runs
- `Continue` restores the exact puzzle state you left, including available undo and redo actions
- `settings.json` — animation speed, nudge highlight preference, and feedback beep setting

## Testing

Run the full suite:

```bash
dotnet test Solution2.sln
```

The automated suite covers:

- geometry and direction primitives
- chain mutation and collision rules
- solver correctness
- chapter validity
- target-shape structural constraints
- progression state behavior
- desktop persistence and settings behavior

Development-only generator/diagnostic files are intentionally excluded from the
normal test project build so CI and local validation stay deterministic.

## CI

GitHub Actions runs:

- restore
- release build
- release test run

on both `ubuntu-latest` and `windows-latest`.

## Project Quality Defaults

- nullable reference types enabled
- implicit usings enabled
- latest recommended analyzer level enabled
- warnings treated as errors
- deterministic builds enabled
- repo-wide formatting via `.editorconfig`
- generated output ignored via `.gitignore`
