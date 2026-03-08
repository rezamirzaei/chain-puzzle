using System.Diagnostics;

namespace ChainPuzzle.Desktop;

internal sealed class AudioFeedbackService
{
    private readonly Lock _gate = new();
    private readonly Dictionary<AudioCue, DateTimeOffset> _lastPlayedAt = new();
    private readonly Dictionary<AudioCue, string> _assetPaths;
    private readonly PlayerCommand? _player;

    public AudioFeedbackService(string? baseDirectory = null)
    {
        var root = baseDirectory ?? AppContext.BaseDirectory;
        _assetPaths = new Dictionary<AudioCue, string>
        {
            [AudioCue.BlockedMove] = Path.Combine(root, "Assets", "Audio", "blocked.wav"),
            [AudioCue.ChapterSolved] = Path.Combine(root, "Assets", "Audio", "solved.wav")
        };
        _player = ResolvePlayer();
    }

    public void Play(AudioCue cue, bool enabled)
    {
        if (!enabled || _player is null || !_assetPaths.TryGetValue(cue, out var assetPath) || !File.Exists(assetPath))
        {
            return;
        }

        if (IsRateLimited(cue))
        {
            return;
        }

        _ = Task.Run(() => PlayCore(assetPath, _player));
    }

    private bool IsRateLimited(AudioCue cue)
    {
        var now = DateTimeOffset.UtcNow;
        var cooldown = cue == AudioCue.BlockedMove
            ? TimeSpan.FromMilliseconds(90)
            : TimeSpan.FromMilliseconds(250);

        lock (_gate)
        {
            if (_lastPlayedAt.TryGetValue(cue, out var lastPlayedAt) && now - lastPlayedAt < cooldown)
            {
                return true;
            }

            _lastPlayedAt[cue] = now;
            return false;
        }
    }

    private static PlayerCommand? ResolvePlayer()
    {
        if (OperatingSystem.IsMacOS())
        {
            return TryResolveExecutable("afplay") is { } afplay
                ? new PlayerCommand(afplay, PlayerKind.Afplay)
                : null;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var candidate in new[] { "powershell", "pwsh" })
            {
                if (TryResolveExecutable(candidate) is { } executable)
                {
                    return new PlayerCommand(executable, PlayerKind.PowerShell);
                }
            }

            return null;
        }

        if (OperatingSystem.IsLinux())
        {
            foreach (var candidate in new[]
                     {
                         new PlayerCommand("ffplay", PlayerKind.Ffplay),
                         new PlayerCommand("paplay", PlayerKind.Paplay),
                         new PlayerCommand("aplay", PlayerKind.Aplay),
                         new PlayerCommand("play", PlayerKind.SoXPlay)
                     })
            {
                if (TryResolveExecutable(candidate.ExecutablePath) is { } executable)
                {
                    return candidate with { ExecutablePath = executable };
                }
            }
        }

        return null;
    }

    private static string? TryResolveExecutable(string executableName)
    {
        if (Path.IsPathRooted(executableName) && File.Exists(executableName))
        {
            return executableName;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, executableName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void PlayCore(string assetPath, PlayerCommand player)
    {
        try
        {
            using var process = Process.Start(CreateStartInfo(player, assetPath));
            if (process is null)
            {
                return;
            }

            if (!process.WaitForExit(5_000))
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Audio is optional polish and must never break gameplay.
        }
    }

    private static ProcessStartInfo CreateStartInfo(PlayerCommand player, string assetPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = player.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        switch (player.Kind)
        {
            case PlayerKind.Afplay:
            case PlayerKind.Paplay:
            case PlayerKind.Aplay:
                startInfo.ArgumentList.Add(assetPath);
                break;
            case PlayerKind.Ffplay:
                startInfo.ArgumentList.Add("-v");
                startInfo.ArgumentList.Add("quiet");
                startInfo.ArgumentList.Add("-nodisp");
                startInfo.ArgumentList.Add("-autoexit");
                startInfo.ArgumentList.Add(assetPath);
                break;
            case PlayerKind.SoXPlay:
                startInfo.ArgumentList.Add("-q");
                startInfo.ArgumentList.Add(assetPath);
                break;
            case PlayerKind.PowerShell:
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add(BuildPowerShellCommand(assetPath));
                break;
            default:
                throw new InvalidOperationException($"Unsupported audio player kind: {player.Kind}.");
        }

        return startInfo;
    }

    private static string BuildPowerShellCommand(string assetPath)
    {
        var escapedPath = assetPath.Replace("'", "''", StringComparison.Ordinal);
        return $"$player = New-Object System.Media.SoundPlayer '{escapedPath}'; $player.PlaySync();";
    }

    private sealed record PlayerCommand(string ExecutablePath, PlayerKind Kind);

    private enum PlayerKind
    {
        Afplay,
        PowerShell,
        Ffplay,
        Paplay,
        Aplay,
        SoXPlay
    }
}
