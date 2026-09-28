using Domolov.Domain.Common;

namespace Domolov.Domain.Auth;

/// <summary>A signed-in browser session of the operator.</summary>
public sealed class OperatorSession
{
    private OperatorSession() { }

    public OperatorSession(
        TimeSpan lifetime,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset now
    )
    {
        Id = Ids.New();
        CreatedAt = now;
        LastSeenAt = now;
        ExpiresAt = now.Add(lifetime);
        UserAgent = Truncate(userAgent, 512);
        IpAddress = Truncate(ipAddress, 64);
    }

    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>Records activity and slides the expiry forward.</summary>
    public void Touch(TimeSpan lifetime, DateTimeOffset now)
    {
        LastSeenAt = now;
        ExpiresAt = now.Add(lifetime);
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static string? Truncate(string? value, int max) =>
        value is null ? null
        : value.Length <= max ? value
        : value[..max];
}
