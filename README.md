# Chain Chapters

Chain Chapters is a .NET 10 desktop puzzle game built with Avalonia.
The player rotates joints in a segmented chain until the chain fully covers a
target silhouette on a hex grid. Each shipped chapter uses the same chain
length, has exactly one full-cover solution, and starts from a sparse,
non-solved state.

## What Is In The Game

- 10 handcrafted chapters with solid, hole-free target silhouettes
- exact per-chapter par counts
- clickable chapter gallery with silhouette previews, medal tracking, par counts, and baked branch-pressure stats
- drag controls, keyboard controls, undo/redo, and text-only or highlighted nudges
- optional expert mode that disables undo, redo, and nudge
- bundled sound effects for blocked moves and chapter solves
- local save data for exact in-progress boards, undo/redo history, chapter progress, and best runs
- local settings for animation speed, hint highlighting, sound effects, and expert mode

## Runtime Architecture

The solution is split into a small number of focused layers:

- `src/ChainPuzzle.Core`
  - pure puzzle logic
  - chain geometry, move rules, solver, target-cover counting, chapter data, and validation
- `src/ChainPuzzle.Desktop`
  - desktop shell
  - `MainWindow` handles rendering, input, and animation timing
- `GameViewModel` owns user-facing state, progress, medals, and settings
- `GameViewModel` also exposes chapter-card data, difficulty readouts, approach text, and expert-mode behavior
- `ChainBoardControl` renders the board and live chain
- `ChainBoardControl` renders the board atmosphere as well as the chain and target
- `ShapePreviewControl` renders chapter silhouettes in the gallery
- baked tree-profile metrics from `ChainPuzzle.Core` are surfaced in the gallery so players can read trap pressure before opening a chapter
- `tests/ChainPuzzle.Tests`
  - gameplay, geometry, solver, desktop-state, and chapter-structure tests

More detail is in [docs/development.md](docs/development.md).

## Solution Layout

- `Solution2.sln` — root solution file
- `global.json` — pinned .NET SDK for reproducible local and CI builds
- `Directory.Build.props` — shared language/analyzer settings
- `.editorconfig` — formatting and style defaults
- `.github/workflows/ci.yml` — build-and-test workflow for push and pull request validation
- `.github/workflows/release-builds.yml` — packaged desktop build workflow for macOS, Windows, and Linux

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
- `settings.json` — animation speed, nudge highlight preference, and sound-effect setting
- `settings.json` also stores whether sound effects and expert mode are enabled

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

Packaged desktop artifacts are produced by the release workflow for:

- `linux-x64`
- `win-x64`
- `osx-arm64`

Run the local publish command for your platform:

```bash
dotnet publish src/ChainPuzzle.Desktop/ChainPuzzle.Desktop.csproj -c Release -r osx-arm64 --self-contained false
```

Or use the helper script on macOS/Linux:

```bash
./scripts/publish-desktop.sh osx-arm64
```

## Project Quality Defaults

- nullable reference types enabled
- implicit usings enabled
- latest recommended analyzer level enabled
- warnings treated as errors
- deterministic builds enabled
- repo-wide formatting via `.editorconfig`
- generated output ignored via `.gitignore`
