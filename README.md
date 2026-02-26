# Chain Chapters (.NET Desktop)

Chain Chapters is a C# desktop chain-puzzle game with chapter progression.
Each chapter uses the same chain length, starts from a scrambled state, and is
validated so it has one and only one solved target state.

## Solution Layout

- `src/ChainPuzzle.Core`: puzzle model, solver, chapter generation, validation.
- `src/ChainPuzzle.Desktop`: Avalonia desktop UI with smooth rotation animation.
- `tests/ChainPuzzle.Tests`: xUnit tests for solver and chapter validity.

## Run Desktop App

```bash
dotnet run --project src/ChainPuzzle.Desktop/ChainPuzzle.Desktop.csproj
```

You can also open `Solution2.sln` in Rider/Visual Studio and run `ChainPuzzle.Desktop`.

## Run Tests

```bash
dotnet test Solution2.sln
```
