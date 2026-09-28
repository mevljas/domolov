using System.Diagnostics;
using FluentAssertions;

namespace Domolov.UnitTests;

/// <summary>Fails when the repo is not CSharpier-clean (same gate as CI).</summary>
public sealed class CSharpierFormatTests
{
    [Fact]
    public void Repository_passes_csharpier_check()
    {
        var root = FindRepoRoot();
        root.Should().NotBeNull("repo root with .config/dotnet-tools.json should be found");

        RunDotnet(root!, ["tool", "restore"]).ExitCode.Should().Be(0);

        var check = RunDotnet(root!, ["csharpier", "check", "."]);
        check
            .ExitCode.Should()
            .Be(
                0,
                "CSharpier check failed. Run: dotnet tool restore && dotnet csharpier format .{0}{1}",
                Environment.NewLine,
                check.Output
            );
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var tools = Path.Combine(dir.FullName, ".config", "dotnet-tools.json");
            if (File.Exists(tools))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static (int ExitCode, string Output) RunDotnet(string workingDirectory, string[] args)
    {
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start);
        process.Should().NotBeNull();
        // Drain both pipes concurrently; reading them one after the other deadlocks once the
        // unread pipe's buffer fills up.
        var stdout = process!.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(300_000))
        {
            process.Kill(entireProcessTree: true);
        }

        return (
            process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult()
        );
    }
}
