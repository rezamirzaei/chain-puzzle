using ChainPuzzle.Core;

namespace ChainPuzzle.Desktop.ViewModels;

/// <summary>
/// Home-overlay card data for a single chapter preview.
/// </summary>
public sealed record ChapterGalleryCard(
    int Index,
    string NumberText,
    string Subtitle,
    string AccentHex,
    IReadOnlyList<IntPoint> TargetPoints,
    bool IsCurrent,
    bool IsCleared,
    string MedalLabel,
    string DifficultyText,
    string MethodText,
    string BestText,
    string PressureText,
    string BranchText);
