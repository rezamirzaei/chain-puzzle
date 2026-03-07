# Chain Chapters (.NET Desktop)

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

## Solution Layout

- `src/ChainPuzzle.Core`: puzzle model, solver, chapter generation, validation.
- `src/ChainPuzzle.Desktop`: Avalonia desktop UI with smooth rotation animation.
- `tests/ChainPuzzle.Tests`: xUnit tests for solver and chapter validity.

## Run Desktop App

```bash
dotnet run --project src/ChainPuzzle.Desktop/ChainPuzzle.Desktop.csproj
```

You can also open `Solution2.sln` in Rider/Visual Studio and run `ChainPuzzle.Desktop`.

## Controls

- Mouse: click a joint and drag to rotate.
- Keyboard: `Up`/`Down` selects a joint, `A`/`D` rotates.
- History: `Ctrl+Z` undoes, `Ctrl+Y` redoes.
- Nudge: the in-game `Nudge` button highlights a recommended joint and direction without playing the move.
- UI: use the chapter picker to jump between chapters.

## Run Tests

```bash
dotnet test Solution2.sln
```
