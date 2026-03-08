# Development Guide

## Core Rule Model

The puzzle is defined by a self-avoiding chain made of fixed-length segments on
a pointy-top axial hex grid.

- `ChainState` holds the segment directions and derived point set.
- `ChainSolver` explores reachable self-avoiding states through joint rotations.
- `TargetCoverCounter` verifies how many ways the fixed segment-length sequence
  can cover a given target silhouette.
- `ChapterFactory` ships the authored chapter data.

The solved condition is not “match one exact hidden pose”. A chapter is solved
when the current chain covers the chapter target point set exactly.

## Chapter Authoring Contract

Each shipped chapter should satisfy all of the following:

- self-avoiding goal state
- self-avoiding start state
- target shape is connected
- target shape has no holes
- target shape is visually solid rather than line-like
- exactly one full-cover solution for the fixed segment lengths
- a sparse start state that does not already resemble a solved board
- a broad reverse tree near the goal so the puzzle has branching pressure

The xUnit suite enforces these constraints directly. If a new chapter fails,
the right fix is to change the chapter data, not to weaken the rule unless the
rule itself is demonstrably wrong.

## Desktop Runtime

The desktop project currently uses a pragmatic split:

- `GameViewModel`
  - progress
  - settings
  - chapter navigation
  - medal computation
  - hint orchestration
- `MainWindow`
  - input events
  - animation timing
  - control synchronization
  - optional sound-effect triggers
- `ChainBoardControl`
  - board rendering
- `ShapePreviewControl`
  - chapter gallery silhouette rendering

That split keeps puzzle logic out of the window while still avoiding a large UI
framework refactor.

## Settings

User settings are stored in `settings.json` through `SettingsStore`.

Current settings:

- animation speed
- whether nudges also highlight the suggested joint
- whether bundled sound effects are enabled

Settings are loaded at startup and saved whenever the user changes them.
The view model suppresses write-back during initial load, so startup does not
rewrite the settings file just to hydrate UI state.

Desktop audio playback is deliberately best-effort. The app ships WAV assets and
uses a small platform-specific player wrapper so missing audio support never
breaks the game loop.

## Progress Data

User progress is stored in `progress.json` through `GameProgressStore`.

Tracked values:

- current chapter index
- current chain state
- current move count
- undo history
- redo history
- cleared chapter ids
- best move count per chapter

Progress and settings writes are atomic, so an interrupted save does not leave
behind a partially-written JSON document. `GameProgressStore` also migrates the
previous version of the progress file forward instead of silently discarding it.

Medals are derived from best move counts:

- gold: par or better
- silver: one move over par
- bronze: cleared above silver

## Test Layout

`ChainCoreTests` covers authored content and gameplay invariants.

`AdditionalCoreTests` covers the lower-level model and solver behavior.

`DesktopStateTests` covers persistence, settings hydration, and view-model-only
desktop logic without needing an Avalonia window.

Generator and diagnostic experiments should not be part of the default CI test
surface. Keep them outside the normal project build or explicitly exclude them.

## Validation Workflow

Use this sequence before committing gameplay or content changes:

```bash
dotnet build Solution2.sln
dotnet test Solution2.sln
```

The solution is configured to treat warnings as errors and uses a pinned SDK
via `global.json`, so local validation should match CI closely.

## Release Packaging

`.github/workflows/release-builds.yml` publishes packaged desktop artifacts for:

- `linux-x64`
- `win-x64`
- `osx-arm64`

Use the same publish shape locally when you need a distributable folder:

```bash
dotnet publish src/ChainPuzzle.Desktop/ChainPuzzle.Desktop.csproj -c Release -r osx-arm64 --self-contained false
```

For local packaging on macOS/Linux, `scripts/publish-desktop.sh` wraps the same
publish command and zips the output when a zip tool is available.

If the change touches chapter data, also check the resulting in-game feel, not
just the tests. Passing structure metrics are necessary, not sufficient.
