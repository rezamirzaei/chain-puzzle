# Chain Chapters (.NET Desktop)

Chain Chapters is a C# desktop chain-puzzle game with chapter progression.
Each chapter uses the same chain length, starts from a scrambled state, and
asks the player to rotate joints until the chain matches the target shape.

## Current Features

- 10 handcrafted chapters with distinct filled target shapes.
- Smooth drag and keyboard rotation controls.
- Undo and redo support.
- Chapter picker with completion tracking.
- Personal best move counts saved locally between sessions.
- Filled board rendering for target shapes instead of peg-only outlines.

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
- UI: use the chapter picker to jump between chapters.

## Run Tests

```bash
dotnet test Solution2.sln
```
