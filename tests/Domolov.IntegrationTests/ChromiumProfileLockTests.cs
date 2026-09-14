using Domolov.Infrastructure.Providers;
using FluentAssertions;

namespace Domolov.IntegrationTests;

public sealed class ChromiumProfileLockTests
{
    [Fact]
    public void TryClearStale_removes_lock_for_foreign_hostname()
    {
        var dir = CreateTempProfile();
        try
        {
            WriteLockTarget(dir, "old-container-59");
            File.WriteAllText(Path.Combine(dir, "SingletonCookie"), "x");
            File.WriteAllText(Path.Combine(dir, "SingletonSocket"), "x");

            var cleared = ChromiumProfileLock.TryClearStale(
                dir,
                currentHostname: "new-container",
                isProcessAlive: _ => true
            );

            cleared.Should().BeTrue();
            File.Exists(Path.Combine(dir, "SingletonLock")).Should().BeFalse();
            File.Exists(Path.Combine(dir, "SingletonCookie")).Should().BeFalse();
            File.Exists(Path.Combine(dir, "SingletonSocket")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryClearStale_keeps_lock_for_live_local_process()
    {
        var dir = CreateTempProfile();
        try
        {
            var hostname = "this-host";
            WriteLockTarget(dir, $"{hostname}-1234");

            var cleared = ChromiumProfileLock.TryClearStale(
                dir,
                currentHostname: hostname,
                isProcessAlive: pid => pid == 1234
            );

            cleared.Should().BeFalse();
            File.Exists(Path.Combine(dir, "SingletonLock")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryClearStale_removes_lock_for_dead_local_pid()
    {
        var dir = CreateTempProfile();
        try
        {
            var hostname = "this-host";
            WriteLockTarget(dir, $"{hostname}-99999");

            var cleared = ChromiumProfileLock.TryClearStale(
                dir,
                currentHostname: hostname,
                isProcessAlive: _ => false
            );

            cleared.Should().BeTrue();
            File.Exists(Path.Combine(dir, "SingletonLock")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ForceClear_removes_all_singleton_files()
    {
        var dir = CreateTempProfile();
        try
        {
            File.WriteAllText(Path.Combine(dir, "SingletonLock"), "anything");
            File.WriteAllText(Path.Combine(dir, "SingletonCookie"), "x");
            ChromiumProfileLock.ForceClear(dir).Should().BeTrue();
            File.Exists(Path.Combine(dir, "SingletonLock")).Should().BeFalse();
            File.Exists(Path.Combine(dir, "SingletonCookie")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LooksLikeProfileInUse_detects_chromium_message()
    {
        var ex = new InvalidOperationException(
            "The profile appears to be in use by another Chromium process (59) on another computer (abc)."
        );
        ChromiumProfileLock.LooksLikeProfileInUse(ex).Should().BeTrue();
        ChromiumProfileLock
            .LooksLikeProfileInUse(new InvalidOperationException("unrelated"))
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData("25bd2f226a7c-59", "25bd2f226a7c", 59)]
    [InlineData("host-with-dashes-42", "host-with-dashes", 42)]
    public void TryParseHostPid_parses_chromium_format(string target, string host, int pid)
    {
        ChromiumProfileLock.TryParseHostPid(target, out var h, out var p).Should().BeTrue();
        h.Should().Be(host);
        p.Should().Be(pid);
    }

    private static string CreateTempProfile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "domolov-chromium-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteLockTarget(string dir, string hostPid)
    {
        // Plain file contents — portable across Windows/Linux test hosts.
        File.WriteAllText(Path.Combine(dir, "SingletonLock"), hostPid);
    }
}
