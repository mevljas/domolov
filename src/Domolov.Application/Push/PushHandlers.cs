using System.ComponentModel.DataAnnotations;
using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Domain.Push;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Push;

/// <summary>A browser push subscription (from PushSubscription.toJSON()).</summary>
public sealed record PushSubscriptionRequest(
    [property: Required, MaxLength(2000), Url] string Endpoint,
    [property: Required, MaxLength(512)] string P256dh,
    [property: Required, MaxLength(512)] string Auth
);

/// <summary>The stored subscription id (used to unsubscribe).</summary>
public sealed record PushSubscriptionResponse(Guid Id);

/// <summary>Creates or refreshes a push subscription by endpoint.</summary>
public sealed class UpsertPushSubscriptionHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<PushSubscriptionResponse> HandleAsync(
        PushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken
    )
    {
        var existing = await db.WebPushSubscriptions.FirstOrDefaultAsync(
            s => s.Endpoint == request.Endpoint,
            cancellationToken
        );
        if (existing is null)
        {
            existing = new WebPushSubscription(
                request.Endpoint,
                request.P256dh,
                request.Auth,
                userAgent,
                clock.GetUtcNow()
            );
            db.WebPushSubscriptions.Add(existing);
        }
        else
        {
            existing.UpdateKeys(request.P256dh, request.Auth);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PushSubscriptionResponse(existing.Id);
    }
}

/// <summary>Removes a push subscription.</summary>
public sealed class DeletePushSubscriptionHandler(IAppDbContext db)
{
    public async Task HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (
            await db
                .WebPushSubscriptions.Where(s => s.Id == id)
                .ExecuteDeleteAsync(cancellationToken) == 0
        )
        {
            throw new NotFoundException("PushSubscription", id);
        }
    }
}
