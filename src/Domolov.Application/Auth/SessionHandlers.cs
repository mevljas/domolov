using System.ComponentModel.DataAnnotations;
using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Options;
using Domolov.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domolov.Application.Auth;

/// <summary>Sign-in payload.</summary>
public sealed record LoginRequest([property: Required, MaxLength(512)] string Password);

/// <summary>The current session.</summary>
public sealed record SessionResponse(
    string Name,
    Guid SessionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt
);

/// <summary>A session in the session list.</summary>
public sealed record SessionListItem(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    string? UserAgent,
    string? IpAddress,
    bool IsCurrent
);

/// <summary>Verifies the operator password and opens sessions.</summary>
public sealed class LoginHandler(
    IAppDbContext db,
    IOptions<DomolovOptions> options,
    TimeProvider clock
)
{
    public const string OperatorName = "admin";

    /// <summary>Returns the new session, or null when the password is wrong or none is configured.</summary>
    public async Task<OperatorSession?> HandleAsync(
        LoginRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidPassword(request.Password))
        {
            return null;
        }

        var session = new OperatorSession(Lifetime, userAgent, ipAddress, clock.GetUtcNow());
        db.OperatorSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public TimeSpan Lifetime => TimeSpan.FromDays(options.Value.SessionLifetimeDays);

    public bool IsValidPassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var o = options.Value;
        if (!string.IsNullOrWhiteSpace(o.AdminPasswordHash))
        {
            return PasswordHashing.Verify(password, o.AdminPasswordHash);
        }

        return !string.IsNullOrEmpty(o.AdminPassword)
            && PasswordHashing.FixedTimeEquals(password, o.AdminPassword);
    }
}

/// <summary>Checks a session on each request and slides its expiry.</summary>
public sealed class SessionValidator(
    IAppDbContext db,
    IOptions<DomolovOptions> options,
    TimeProvider clock
)
{
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    public async Task<bool> ValidateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await db.OperatorSessions.FirstOrDefaultAsync(
            s => s.Id == sessionId,
            cancellationToken
        );
        var now = clock.GetUtcNow();
        if (session is null || !session.IsActive(now))
        {
            return false;
        }

        if (now - session.LastSeenAt >= TouchInterval)
        {
            session.Touch(TimeSpan.FromDays(options.Value.SessionLifetimeDays), now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}

/// <summary>Returns the current session.</summary>
public sealed class GetSessionHandler(IAppDbContext db)
{
    public async Task<SessionResponse> HandleAsync(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var session =
            await db
                .OperatorSessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException("Session", sessionId);
        return new SessionResponse(
            LoginHandler.OperatorName,
            session.Id,
            session.CreatedAt,
            session.ExpiresAt
        );
    }
}

/// <summary>Lists active sessions.</summary>
public sealed class ListSessionsHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<SessionListItem>> HandleAsync(
        Guid currentId,
        CancellationToken cancellationToken
    )
    {
        var now = clock.GetUtcNow();
        return await db
            .OperatorSessions.AsNoTracking()
            .Where(s => s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new SessionListItem(
                s.Id,
                s.CreatedAt,
                s.LastSeenAt,
                s.ExpiresAt,
                s.UserAgent,
                s.IpAddress,
                s.Id == currentId
            ))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Revokes one session, or every session ("sign out everywhere").</summary>
public sealed class RevokeSessionsHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session =
            await db.OperatorSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException("Session", sessionId);
        session.Revoke(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RevokeAllAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await db
            .OperatorSessions.Where(s => s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }
}
