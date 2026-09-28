using Domolov.Domain.Common;

namespace Domolov.Domain.Push;

/// <summary>A browser Web Push subscription.</summary>
public sealed class WebPushSubscription
{
    private WebPushSubscription()
    {
        Endpoint = "";
        P256dh = "";
        Auth = "";
    }

    public WebPushSubscription(
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        DateTimeOffset now
    )
    {
        Id = Ids.New();
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Endpoint { get; private set; }
    public string P256dh { get; private set; }
    public string Auth { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateKeys(string p256dh, string auth)
    {
        P256dh = p256dh;
        Auth = auth;
    }
}
