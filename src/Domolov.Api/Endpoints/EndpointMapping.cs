using Domolov.Api.Auth;
using Domolov.Application.Options;
using Domolov.Infrastructure.Scanning;
using Microsoft.Extensions.Options;

namespace Domolov.Api.Endpoints;

/// <summary>Maps every /api endpoint group.</summary>
public static class EndpointMapping
{
    public static void MapDomolovEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter<CsrfHeaderFilter>();
        api.MapSessionEndpoints();
        api.MapWatchEndpoints();
        api.MapNotificationRouteEndpoints();
        api.MapHomeEndpoints();
        api.MapListingEndpoints();
        api.MapScanEndpoints();
        api.MapSystemEndpoints();

        if (app.Services.GetRequiredService<IOptions<DomolovOptions>>().Value.FakeProvider)
        {
            app.MapGet(
                    "/api/demo/photos/{id:regex(^p[0-9]{{1,3}}$)}.svg",
                    (string id) => Results.Text(DemoPhotos.Svg(id), "image/svg+xml")
                )
                .AllowAnonymous()
                .ExcludeFromDescription();
        }
    }
}
