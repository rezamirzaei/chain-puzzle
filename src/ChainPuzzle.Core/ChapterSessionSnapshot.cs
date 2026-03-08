namespace ChainPuzzle.Core;

/// <summary>
/// A saved chapter history frame, containing a board state and the move counter at that point.
/// </summary>
/// <param name="State">The chain state for this frame.</param>
/// <param name="Moves">The move count associated with the frame.</param>
public sealed record ChapterHistorySnapshot(ChainState State, int Moves);

/// <summary>
/// A complete restorable chapter session, including the current state and undo/redo history.
/// </summary>
/// <param name="LevelIndex">The current level index.</param>
/// <param name="CurrentState">The current board state.</param>
/// <param name="Moves">The current move count.</param>
/// <param name="UndoHistory">Undo history ordered from most recent to oldest.</param>
/// <param name="RedoHistory">Redo history ordered from most recent to oldest.</param>
public sealed record ChapterSessionSnapshot(
    int LevelIndex,
    ChainState CurrentState,
    int Moves,
    IReadOnlyList<ChapterHistorySnapshot> UndoHistory,
    IReadOnlyList<ChapterHistorySnapshot> RedoHistory);
