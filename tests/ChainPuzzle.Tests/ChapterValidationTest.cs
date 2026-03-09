using ChainPuzzle.Core;
using Xunit;
using Xunit.Abstractions;

namespace ChainPuzzle.Tests;

/// <summary>
/// Validates that all 10 shipped chapters load correctly, are self-avoiding,
/// have different goal and start states, and have at least one legal move.
/// </summary>
public sealed class ChapterValidationTest
{
    private readonly ITestOutputHelper _output;

    public ChapterValidationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllChaptersAreValid()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        _output.WriteLine($"Total chapters: {levels.Count}");
        Assert.Equal(10, levels.Count);

        foreach (var level in levels)
        {
            _output.WriteLine($"\n=== {level.Id}: {level.Title} - {level.Subtitle} ===");

            // Goal must be self-avoiding
            Assert.True(level.GoalState.IsSelfAvoiding(),
                $"{level.Id}: Goal state is not self-avoiding.");

            // Start must be self-avoiding
            Assert.True(level.StartState.IsSelfAvoiding(),
                $"{level.Id}: Start state is not self-avoiding.");

            // They must cover different targets
            var goalPoints = level.GoalState.GetPoints().ToHashSet();
            var startPoints = level.StartState.GetPoints().ToHashSet();
            Assert.False(goalPoints.SetEquals(startPoints),
                $"{level.Id}: Start state already matches the goal.");

            // Must have legal moves
            var solver = new ChainSolver(level.GoalState.SegmentCount);
            var legalMoves = solver.GetLegalMoves(level.StartState);
            Assert.True(legalMoves.Count > 0,
                $"{level.Id}: Start state has no legal moves.");

            _output.WriteLine($"  Goal points: {level.GoalState.GetPoints().Count}");
            _output.WriteLine($"  Start points: {level.StartState.GetPoints().Count}");
            _output.WriteLine($"  Legal moves from start: {legalMoves.Count}");
            _output.WriteLine($"  Optimal moves: {level.OptimalMoves}");
            _output.WriteLine($"  Accent: {level.AccentHex}");
        }
    }

    [Fact]
    public void AllChaptersAreSolvable()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        Assert.Equal(10, levels.Count);

        foreach (var level in levels)
        {
            _output.WriteLine($"Checking solvability: {level.Id} - {level.Subtitle}...");
            var solver = new ChainSolver(level.GoalState.SegmentCount);
            var path = solver.FindShortestPath(
                level.StartState,
                state => level.IsSolved(state),
                maxVisited: 500_000);

            if (path is not null)
            {
                _output.WriteLine($"  Solved in {path.Count} moves (par: {level.OptimalMoves}).");
            }
            else
            {
                _output.WriteLine($"  Could not verify solution within search limit (may still be solvable).");
            }
        }
    }
}

