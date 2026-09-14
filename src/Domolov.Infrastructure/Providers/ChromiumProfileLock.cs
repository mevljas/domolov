using System.Diagnostics;

namespace Domolov.Infrastructure.Providers;

/// <summary>
/// Clears Chromium Singleton* files left on a persistent profile volume after container recreate.
/// </summary>
public static class ChromiumProfileLock
{
    private static readonly string[] LockFileNames =
    [
        "SingletonLock",
        "SingletonCookie",
        "SingletonSocket",
    ];

    /// <summary>
    /// Deletes Singleton* files when the lock points at another hostname or a dead PID.
    /// </summary>
    /// <returns><c>true</c> if lock files were removed.</returns>
    public static bool TryClearStale(
        string userDataDir,
        string? currentHostname = null,
        Func<int, bool>? isProcessAlive = null
    )
    {
        if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
        {
            return false;
        }

        var lockPath = Path.Combine(userDataDir, "SingletonLock");
        if (!File.Exists(lockPath) && !IsSymlink(lockPath))
        {
            // No lock file — nothing to clear (Cookie/Socket alone are harmless).
            return false;
        }

        if (
            !TryReadLockTarget(lockPath, out var target)
            || !TryParseHostPid(target, out var host, out var pid)
        )
        {
            // Unreadable / unparseable lock after a crash — safe to clear.
            return ForceClear(userDataDir);
        }

        var hostname = currentHostname ?? Environment.MachineName;
        var alive = isProcessAlive ?? DefaultIsProcessAlive;

        var stale =
            !string.Equals(host, hostname, StringComparison.OrdinalIgnoreCase) || !alive(pid);

        return stale && ForceClear(userDataDir);
    }

    /// <summary>Unconditionally removes Chromium singleton lock files.</summary>
    public static bool ForceClear(string userDataDir)
    {
        if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
        {
            return false;
        }

        var removed = false;
        foreach (var name in LockFileNames)
        {
            var path = Path.Combine(userDataDir, name);
            try
            {
                if (File.Exists(path) || IsSymlink(path))
                {
                    File.Delete(path);
                    removed = true;
                }
            }
            catch (IOException)
            {
                // Best-effort; launch may still fail.
            }
            catch (UnauthorizedAccessException) { }
        }

        return removed;
    }

    public static bool LooksLikeProfileInUse(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            var message = ex.Message;
            if (
                message.Contains("profile appears to be in use", StringComparison.OrdinalIgnoreCase)
                || message.Contains("SingletonLock", StringComparison.OrdinalIgnoreCase)
                || message.Contains("process_singleton", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryParseHostPid(string target, out string host, out int pid)
    {
        host = "";
        pid = 0;
        var idx = target.LastIndexOf('-');
        if (idx <= 0 || idx >= target.Length - 1)
        {
            return false;
        }

        host = target[..idx];
        return int.TryParse(target[(idx + 1)..], out pid) && pid > 0;
    }

    private static bool TryReadLockTarget(string lockPath, out string target)
    {
        target = "";
        try
        {
            var info = new FileInfo(lockPath);
            if (info.LinkTarget is { Length: > 0 } link)
            {
                target = Path.GetFileName(link.Trim());
                if (string.IsNullOrEmpty(target))
                {
                    target = link.Trim();
                }

                return target.Length > 0;
            }

            // Some builds store the target as file contents.
            if (File.Exists(lockPath))
            {
                target = File.ReadAllText(lockPath).Trim();
                return target.Length > 0;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool DefaultIsProcessAlive(int pid)
    {
        if (OperatingSystem.IsLinux())
        {
            return Directory.Exists($"/proc/{pid}");
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
