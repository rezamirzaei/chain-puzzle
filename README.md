# Chain Chapters (.NET Desktop)

[![CI](../../actions/workflows/ci.yml/badge.svg)](../../actions/workflows/ci.yml)

Chain Chapters is a C# desktop chain-puzzle game with chapter progression.
Each chapter uses the same chain length, starts from a deceptive scrambled
state, and asks the player to rotate joints until the chain fully covers the
target silhouette.

## Current Features

- 10 handcrafted chapters with distinct filled target shapes, unique full-cover solutions, and deceptive six-move starts.
- Smooth drag and keyboard rotation controls.
- Undo and redo support.
- Chapter picker with completion tracking.
- Real per-chapter par counts baked into the shipped level data.
- Personal best move counts saved locally between sessions.
- Start/menu overlay with continue and clean new-run flow.
- Filled board rendering for target shapes instead of peg-only outlines.
- Reverse-shell tuning so most starts already look close to solved but still branch into many wrong continuations.
- MVVM architecture with `GameViewModel` managing all game state.
- Settings persistence (animation speed, sound, hint highlights).
- Comprehensive XML documentation on all public Core APIs.

## Architecture

```
┌──────────────────┐     ┌─────────────────────────────┐
│ ChainPuzzle.Core │◄────│   ChainPuzzle.Desktop       │
│   (model/solver) │     │  ┌─────────────────────┐    │
│                  │     │  │   GameViewModel      │    │
│  ChainState      │     │  │   (MVVM commands)    │    │
│  ChainSolver     │     │  └──────────┬──────────┘    │
│  ChapterGame     │     │  ┌──────────▼──────────┐    │
│  ChapterFactory  │     │  │   MainWindow         │    │
│  TargetCoverCtr  │     │  │   (render/input)     │    │
│  LevelValidator  │     │  └──────────┬──────────┘    │
│  LevelAnalyzer   │     │  ┌──────────▼──────────┐    │
│                  │     │  │ ChainBoardControl    │    │
└──────────────────┘     │  │ (hex-grid drawing)   │    │
                         │  └─────────────────────┘    │
┌──────────────────┐     └─────────────────────────────┘
│ ChainPuzzle.Tests│
│  (xUnit, 40+     │
│   structural      │
│   tests)          │
└──────────────────┘
```

## Solution Layout

- `src/ChainPuzzle.Core` — puzzle model, solver, chapter generation, validation, XML-documented API.
- `src/ChainPuzzle.Desktop` — Avalonia desktop UI with MVVM architecture and smooth rotation animation.
- `tests/ChainPuzzle.Tests` — xUnit tests for solver, chapter validity, game state, edge cases, and structural invariants.
- `.github/workflows/ci.yml` — GitHub Actions CI pipeline (build, test, coverage).

## Run Desktop App

```bash
dotnet run --project src/ChainPuzzle.Desktop/ChainPuzzle.Desktop.csproj
```

You can also open `Solution2.sln` in Rider/Visual Studio and run `ChainPuzzle.Desktop`.

## Controls

- **Mouse**: click a joint and drag to rotate.
- **Keyboard**: `Up`/`Down` selects a joint, `A`/`D` rotates.
- **History**: `Ctrl+Z` undoes, `Ctrl+Y` or `Ctrl+Shift+Z` redoes.
- **Nudge**: the in-game `Nudge` button highlights a recommended joint and direction without playing the move.
- **UI**: use the chapter picker to jump between chapters.
- **Menu**: `Esc` opens/closes the pause menu. `N` starts a new run from the menu.

## Settings

Settings are persisted in `~/.local/share/ChainPuzzle/settings.json` (or equivalent) and include:
- **Animation speed** (slow / normal / fast)
- **Sound enabled** toggle
- **Hint highlights** toggle

## Run Tests

```bash
dotnet test Solution2.sln
```

## CI

The project uses GitHub Actions for continuous integration. The pipeline:
1. Restores dependencies
2. Builds in Release mode
3. Runs all tests with code coverage collection
4. Uploads coverage artifacts

## Hex Coordinate System

The game uses **axial hex coordinates** (pointy-top orientation):

- Each cell is identified by an `IntPoint(q, r)` where `q` = column, `r` = row.
- The six directions map to: E `(+1,0)`, NE `(+1,−1)`, NW `(0,−1)`, W `(−1,0)`, SW `(−1,+1)`, SE `(0,+1)`.
- Screen projection: `screenX = q + r * 0.5`, `screenY = r * √3/2`.
- The chain starts at the origin `(0,0)` and extends segment-by-segment along these directions.

## Code Style

The repository includes an `.editorconfig` with standard C# style rules and a `Directory.Build.props` with shared nullable, implicit usings, and analysis-level settings applied to all projects.

