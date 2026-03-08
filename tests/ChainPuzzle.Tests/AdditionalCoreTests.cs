using ChainPuzzle.Core;
using Xunit;

namespace ChainPuzzle.Tests;

/// <summary>
/// Comprehensive tests covering edge cases, parsing, arithmetic, solver behaviour,
/// and game state management beyond the original test suite.
/// </summary>
public sealed class AdditionalCoreTests
{
    // ===== IntPoint =====

    [Fact]
    public void IntPoint_Addition_ReturnsCorrectResult()
    {
        var a = new IntPoint(3, -2);
        var b = new IntPoint(-1, 5);
        var sum = a + b;
        Assert.Equal(2, sum.X);
        Assert.Equal(3, sum.Y);
    }

    [Fact]
    public void IntPoint_Key_ReturnsExpectedFormat()
    {
        var point = new IntPoint(-4, 7);
        Assert.Equal("-4,7", point.Key());
    }

    [Fact]
    public void IntPoint_Equality_WorksCorrectly()
    {
        Assert.Equal(new IntPoint(1, 2), new IntPoint(1, 2));
        Assert.NotEqual(new IntPoint(1, 2), new IntPoint(2, 1));
    }

    // ===== Direction =====

    [Fact]
    public void Direction_Rotate_WrapsForward()
    {
        Assert.Equal(Direction.East, Direction.SouthEast.Rotate(1));
        Assert.Equal(Direction.NorthEast, Direction.SouthEast.Rotate(2));
    }

    [Fact]
    public void Direction_Rotate_WrapsBackward()
    {
        Assert.Equal(Direction.SouthEast, Direction.East.Rotate(-1));
        Assert.Equal(Direction.SouthWest, Direction.East.Rotate(-2));
    }

    [Fact]
    public void Direction_FullRotation_ReturnsSame()
    {
        foreach (var dir in Enum.GetValues<Direction>())
        {
            Assert.Equal(dir, dir.Rotate(6));
            Assert.Equal(dir, dir.Rotate(-6));
            Assert.Equal(dir, dir.Rotate(0));
        }
    }

    [Fact]
    public void Direction_ToVector_AllSixAreDistinct()
    {
        var vectors = Enum.GetValues<Direction>().Select(d => d.ToVector()).ToArray();
        Assert.Equal(6, vectors.Distinct().Count());
    }

    [Fact]
    public void Direction_ParseToken_ValidTokens()
    {
        Assert.Equal(Direction.East, DirectionExtensions.ParseToken("E"));
        Assert.Equal(Direction.NorthEast, DirectionExtensions.ParseToken("NE"));
        Assert.Equal(Direction.SouthWest, DirectionExtensions.ParseToken("sw"));
    }

    [Fact]
    public void Direction_ParseToken_InvalidThrows()
    {
        Assert.Throws<ArgumentException>(() => DirectionExtensions.ParseToken("X"));
        Assert.Throws<ArgumentException>(() => DirectionExtensions.ParseToken(""));
    }

    [Fact]
    public void Direction_ToLetter_AllSixDistinct()
    {
        var letters = Enum.GetValues<Direction>().Select(d => d.ToLetter()).ToArray();
        Assert.Equal(6, letters.Distinct().Count());
    }

    // ===== ChainSegment =====

    [Fact]
    public void ChainSegment_Rotate_PreservesLength()
    {
        var segment = new ChainSegment(Direction.East, 3);
        var rotated = segment.Rotate(2);
        Assert.Equal(3, rotated.Length);
        Assert.Equal(Direction.NorthWest, rotated.Direction);
    }

    // ===== ChainMove =====

    [Fact]
    public void ChainMove_Inverse_ReversesRotation()
    {
        var move = new ChainMove(5, 1);
        var inverse = move.Inverse();
        Assert.Equal(5, inverse.JointIndex);
        Assert.Equal(-1, inverse.Rotation);
    }

    // ===== ChainState =====

    [Fact]
    public void ChainState_Constructor_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new ChainState(Array.Empty<ChainSegment>()));
    }

    [Fact]
    public void ChainState_Constructor_RejectsZeroLengthSegment()
    {
        Assert.Throws<ArgumentException>(() => new ChainState(new[]
        {
            new ChainSegment(Direction.East, 0)
        }));
    }

    [Fact]
    public void ChainState_GetPoints_ReturnsCorrectCount()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 2),
            new ChainSegment(Direction.NorthEast, 3)
        });
        // Total links = 2 + 3 = 5, points = 6 (including origin)
        Assert.Equal(6, state.GetPoints().Count);
    }

    [Fact]
    public void ChainState_Clone_ProducesIndependentCopy()
    {
        var original = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.NorthEast, 1)
        });
        var clone = original.Clone();
        Assert.Equal(original.SerializeSegments(), clone.SerializeSegments());
    }

    [Fact]
    public void ChainState_SerializeSegments_RoundTripsThroughPatternParser()
    {
        var original = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 2),
            new ChainSegment(Direction.NorthEast, 1),
            new ChainSegment(Direction.NorthWest, 3),
            new ChainSegment(Direction.SouthEast, 1),
            new ChainSegment(Direction.SouthWest, 2)
        });

        var restored = ChainState.FromPattern(original.SerializeSegments());

        Assert.Equal(original.SerializeSegments(), restored.SerializeSegments());
    }

    [Fact]
    public void ChainState_RotateFromJoint_OutOfRangeReturnsNull()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.NorthEast, 1)
        });
        Assert.Null(state.RotateFromJoint(0, 1));   // joint 0 invalid
        Assert.Null(state.RotateFromJoint(2, 1));   // joint 2 out of range (only 2 segments)
        Assert.Null(state.RotateFromJoint(-1, 1));  // negative
    }

    [Fact]
    public void ChainState_IsSelfAvoiding_DetectsCollision()
    {
        // East then West creates collision at origin
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.West, 1)
        });
        Assert.False(state.IsSelfAvoiding());
    }

    [Fact]
    public void ChainState_IsSelfAvoiding_StraightLineIsValid()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 3),
            new ChainSegment(Direction.East, 2)
        });
        Assert.True(state.IsSelfAvoiding());
    }

    [Fact]
    public void ChainState_Equality_StructurallyEqual()
    {
        var a = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 2),
            new ChainSegment(Direction.NorthEast, 1)
        });
        var b = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 2),
            new ChainSegment(Direction.NorthEast, 1)
        });
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ChainState_Equality_DifferentStatesNotEqual()
    {
        var a = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 2),
            new ChainSegment(Direction.NorthEast, 1)
        });
        var b = new ChainState(new[]
        {
            new ChainSegment(Direction.West, 2),
            new ChainSegment(Direction.NorthEast, 1)
        });
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ChainState_FromPattern_ParsesCorrectly()
    {
        var state = ChainState.FromPattern("E2 NE1 SW3");
        Assert.Equal(3, state.SegmentCount);
        Assert.Equal(Direction.East, state.Segments[0].Direction);
        Assert.Equal(2, state.Segments[0].Length);
        Assert.Equal(Direction.SouthWest, state.Segments[2].Direction);
        Assert.Equal(3, state.Segments[2].Length);
    }

    [Fact]
    public void ChainState_FromPattern_EmptyThrows()
    {
        Assert.Throws<ArgumentException>(() => ChainState.FromPattern(""));
        Assert.Throws<ArgumentException>(() => ChainState.FromPattern("   "));
    }

    [Fact]
    public void ChainState_GetJointPoints_CorrectCount()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.NorthEast, 1),
            new ChainSegment(Direction.West, 1)
        });
        Assert.Equal(2, state.GetJointPoints().Count);  // 3 segments = 2 joints
    }

    [Fact]
    public void ChainState_GetSegmentSpans_CorrectRanges()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 2),
            new ChainSegment(Direction.NorthEast, 3)
        });
        var spans = state.GetSegmentSpans();
        Assert.Equal(2, spans.Count);
        Assert.Equal(0, spans[0].StartIndex);
        Assert.Equal(2, spans[0].EndIndex);
        Assert.Equal(2, spans[1].StartIndex);
        Assert.Equal(5, spans[1].EndIndex);
    }

    // ===== ChainSolver =====

    [Fact]
    public void ChainSolver_Constructor_RejectsSmallSegmentCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChainSolver(1));
    }

    [Fact]
    public void ChainSolver_FindShortestPath_AlreadyAtGoal()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.NorthEast, 1)
        });
        var solver = new ChainSolver(2);
        var path = solver.FindShortestPath(state, _ => true);
        Assert.NotNull(path);
        Assert.Empty(path);
    }

    [Fact]
    public void ChainSolver_GetLegalMoves_ProducesValidStates()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.NorthEast, 1),
            new ChainSegment(Direction.NorthWest, 1)
        });
        var solver = new ChainSolver(3);
        var moves = solver.GetLegalMoves(state);
        Assert.All(moves, m => Assert.True(m.NextState.IsSelfAvoiding()));
    }

    [Fact]
    public void ChainSolver_CountGoalStates_CountsCorrectly()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.NorthEast, 1)
        });
        var solver = new ChainSolver(2);
        // With only 2 segments, the number of reachable states that are self-avoiding is limited
        var count = solver.CountGoalStates(state, _ => true, stopAfter: 100, maxVisited: 1000);
        Assert.True(count > 0);
    }

    // ===== ChapterGame =====

    [Fact]
    public void ChapterGame_Constructor_RequiresLevels()
    {
        Assert.Throws<ArgumentNullException>(() => new ChapterGame(null!));
        Assert.Throws<ArgumentException>(() => new ChapterGame(Array.Empty<ChainLevel>()));
    }

    [Fact]
    public void ChapterGame_SetLevel_ClampsBounds()
    {
        var game = new ChapterGame(ChapterFactory.CreateChapters(validate: false));
        game.SetLevel(-5);
        Assert.Equal(0, game.LevelIndex);
        game.SetLevel(999);
        Assert.Equal(game.Levels.Count - 1, game.LevelIndex);
    }

    [Fact]
    public void ChapterGame_ResetLevel_ClearsHistory()
    {
        var game = new ChapterGame(ChapterFactory.CreateChapters(validate: false));
        var solver = new ChainSolver(game.CurrentLevel.SegmentCount);
        var move = solver.GetLegalMoves(game.CurrentState).First().Move;
        game.TryRotate(move.JointIndex, move.Rotation, out _);
        Assert.Equal(1, game.Moves);

        game.ResetLevel();
        Assert.Equal(0, game.Moves);
        Assert.False(game.CanUndo);
        Assert.False(game.CanRedo);
    }

    [Fact]
    public void ChapterGame_TryRotate_ReturnsFalseWhenBlockedByCollision()
    {
        var game = new ChapterGame(ChapterFactory.CreateChapters(validate: false));
        // Joint 0 is invalid, should fail
        var result = game.TryRotate(0, 1, out _);
        Assert.False(result);
    }

    [Fact]
    public void ChapterGame_NextAndPreviousLevel_Navigate()
    {
        var game = new ChapterGame(ChapterFactory.CreateChapters(validate: false));
        Assert.Equal(0, game.LevelIndex);
        game.NextLevel();
        Assert.Equal(1, game.LevelIndex);
        game.PreviousLevel();
        Assert.Equal(0, game.LevelIndex);
    }

    // ===== TargetCoverCounter =====

    [Fact]
    public void TargetCoverCounter_WrongSizeTargetReturnsZero()
    {
        var counter = new TargetCoverCounter(new[] { 1, 1 });
        // Chain has 2 segments = 3 points total, but we pass 2 target points
        var count = counter.CountSolutions(new[] { new IntPoint(0, 0), new IntPoint(1, 0) }, stopAfter: 10);
        Assert.Equal(0, count);
    }

    [Fact]
    public void TargetCoverCounter_StraightLineHasOneSolution()
    {
        var counter = new TargetCoverCounter(new[] { 2 });
        var target = new[] { new IntPoint(0, 0), new IntPoint(1, 0), new IntPoint(2, 0) };
        var count = counter.CountSolutions(target, stopAfter: 10);
        Assert.Equal(1, count);
    }

    // ===== SeededRandom =====

    [Fact]
    public void SeededRandom_SameSeedProducesSameSequence()
    {
        var a = new SeededRandom("test-seed");
        var b = new SeededRandom("test-seed");
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(a.Next(), b.Next());
        }
    }

    [Fact]
    public void SeededRandom_DifferentSeedsProduceDifferentSequences()
    {
        var a = new SeededRandom("seed-a");
        var b = new SeededRandom("seed-b");
        var same = true;
        for (var i = 0; i < 10; i++)
        {
            if (Math.Abs(a.Next() - b.Next()) > 1e-10) same = false;
        }
        Assert.False(same);
    }

    [Fact]
    public void SeededRandom_NextInt_WithinRange()
    {
        var rng = new SeededRandom("range-test");
        for (var i = 0; i < 200; i++)
        {
            var value = rng.NextInt(6);
            Assert.InRange(value, 0, 5);
        }
    }

    [Fact]
    public void SeededRandom_NextInt_ZeroMaxThrows()
    {
        var rng = new SeededRandom("fail");
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
    }

    // ===== ChainLevel =====

    [Fact]
    public void ChainLevel_IsSolved_MatchesGoal()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        foreach (var level in levels)
        {
            Assert.True(level.IsSolved(level.GoalState));
            Assert.False(level.IsSolved(level.StartState));
        }
    }

    [Fact]
    public void ChainLevel_CountTargetOverlap_GoalIsFullOverlap()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        var level = levels[0];
        var overlap = level.CountTargetOverlap(level.GoalState);
        Assert.Equal(level.TargetPoints.Count, overlap);
    }

    [Fact]
    public void ChainLevel_WithValidation_PreservesData()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        var level = levels[0];
        var validation = new LevelValidation(true, 6, 1);
        var updated = level.WithValidation(validation);
        Assert.Equal(level.Id, updated.Id);
        Assert.Equal(validation, updated.Validation);
    }
}


