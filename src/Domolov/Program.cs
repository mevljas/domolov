using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Domolov.Application.Abstractions;
using Domolov.Application.Contracts;
using Domolov.Application.Services;
using Domolov.Components;
using Domolov.Infrastructure;
using Domolov.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (ctx, cfg) =>
    {
        var level = ctx.Configuration["DOMOLOV_LOG_LEVEL"] ?? "Information";
        if (!Enum.TryParse<Serilog.Events.LogEventLevel>(level, true, out var parsed))
        {
            parsed = Serilog.Events.LogEventLevel.Information;
        }

        cfg.ReadFrom.Configuration(ctx.Configuration)
            .MinimumLevel.Is(parsed)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/domolov-.log", rollingInterval: RollingInterval.Day);
    }
);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Persist Data Protection keys so cookie auth tickets survive container restarts.
var dataProtectionKeysDir = new DirectoryInfo(
    builder.Configuration["DOMOLOV_DATA_PROTECTION_KEYS_DIR"] ?? "/data/data-protection-keys"
);
Directory.CreateDirectory(dataProtectionKeysDir.FullName);
builder
    .Services.AddDataProtection()
    .PersistKeysToFileSystem(dataProtectionKeysDir)
    .SetApplicationName("Domolov");

builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "domolov_auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        // Docker / self-host often runs plain HTTP on :8080.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddDomolovInfrastructure(builder.Configuration);

var otel = builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService(serviceName: builder.Environment.ApplicationName)
    )
    .WithTracing(tracing =>
        tracing
            .AddAspNetCoreInstrumentation(o =>
            {
                o.Filter = http => !http.Request.Path.StartsWithSegments("/healthz");
            })
            .AddHttpClientInstrumentation(o => o.RecordException = true)
            .AddSource("Domolov.Scans")
            .AddSource("Domolov.Notifications")
    )
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    otel.WithTracing(t => t.AddOtlpExporter()).WithMetrics(m => m.AddOtlpExporter());
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DomolovDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
}

var supportedCultures = new[] { new CultureInfo("sl-SI"), new CultureInfo("en") };
app.UseRequestLocalization(
    new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("sl-SI"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures,
        RequestCultureProviders =
        [
            new CookieRequestCultureProvider(),
            new AcceptLanguageHeaderRequestCultureProvider(),
        ],
    }
);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
else
{
    app.MapOpenApi();
}

// Self-host Docker typically binds HTTP only (ASPNETCORE_URLS=http://+:8080).
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty;
if (urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();
app.UseStatusCodePages();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Cookie login for the Blazor login form (full document POST + antiforgery).
app.MapPost(
        "/auth/login",
        async Task<IResult> (
            HttpContext http,
            IOptions<DomolovOptions> options,
            [FromForm] string password
        ) =>
        {
            if (
                string.IsNullOrEmpty(options.Value.AdminPassword)
                || password != options.Value.AdminPassword
            )
            {
                return Results.Redirect("/login?error=true");
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin")],
                CookieAuthenticationDefaults.AuthenticationScheme
            );
            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14),
                }
            );
            return Results.Redirect("/");
        }
    )
    .AllowAnonymous()
    .WithName("CookieLogin")
    .WithSummary("Sign in via HTML form and set the auth cookie");

// Cookie logout for the Blazor nav form (full document POST + antiforgery).
app.MapPost(
        "/auth/logout",
        async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }
    )
    .AllowAnonymous()
    .WithName("CookieLogout")
    .WithSummary("Sign out via HTML form and clear the auth cookie");

var api = app.MapGroup("/api").RequireAuthorization();

api.MapPost(
        "/auth/login",
        async Task<Results<Ok, UnauthorizedHttpResult>> (
            LoginRequest request,
            HttpContext http,
            IOptions<DomolovOptions> options
        ) =>
        {
            if (
                string.IsNullOrEmpty(options.Value.AdminPassword)
                || request.Password != options.Value.AdminPassword
            )
            {
                return TypedResults.Unauthorized();
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin")],
                CookieAuthenticationDefaults.AuthenticationScheme
            );
            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14),
                }
            );
            return TypedResults.Ok();
        }
    )
    .AllowAnonymous()
    .WithName("Login")
    .WithSummary("Sign in with the admin password");

api.MapPost(
        "/auth/logout",
        async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.NoContent();
        }
    )
    .WithName("Logout");

api.MapGet(
        "/watches",
        async Task<Ok<IReadOnlyList<WatchResponse>>> (
            IWatchService watches,
            CancellationToken ct
        ) => TypedResults.Ok(await watches.GetAllAsync(ct))
    )
    .WithName("ListWatches");

api.MapGet(
        "/watches/{id:guid}",
        async Task<Results<Ok<WatchResponse>, NotFound>> (
            Guid id,
            IWatchService watches,
            CancellationToken ct
        ) =>
        {
            var watch = await watches.GetByIdAsync(id, ct);
            return watch is null ? TypedResults.NotFound() : TypedResults.Ok(watch);
        }
    )
    .WithName("GetWatch");

api.MapPost(
        "/watches",
        async Task<Created<WatchResponse>> (
            CreateWatchRequest request,
            IWatchService watches,
            CancellationToken ct
        ) =>
        {
            var created = await watches.CreateAsync(request, ct);
            return TypedResults.Created($"/api/watches/{created.Id}", created);
        }
    )
    .WithName("CreateWatch");

api.MapPut(
        "/watches/{id:guid}",
        async Task<Results<Ok<WatchResponse>, NotFound>> (
            Guid id,
            UpdateWatchRequest request,
            IWatchService watches,
            CancellationToken ct
        ) =>
        {
            var updated = await watches.UpdateAsync(id, request, ct);
            return updated is null ? TypedResults.NotFound() : TypedResults.Ok(updated);
        }
    )
    .WithName("UpdateWatch");

api.MapDelete(
        "/watches/{id:guid}",
        async Task<Results<NoContent, NotFound>> (
            Guid id,
            IWatchService watches,
            CancellationToken ct
        ) => await watches.DeleteAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound()
    )
    .WithName("DeleteWatch");

api.MapPost(
        "/watches/{id:guid}/run",
        async Task<Results<Accepted<Guid>, NotFound>> (
            Guid id,
            IWatchService watches,
            IScanOrchestrator orchestrator,
            CancellationToken ct
        ) =>
        {
            if (await watches.GetByIdAsync(id, ct) is null)
            {
                return TypedResults.NotFound();
            }

            var runId = await orchestrator.EnqueueAsync(id, ct, force: true);
            return TypedResults.Accepted($"/api/scans/{runId}", runId);
        }
    )
    .WithName("RunWatch");

api.MapPost(
        "/watches/{id:guid}/routes",
        async Task<Results<Created<NotificationRouteResponse>, NotFound>> (
            Guid id,
            CreateNotificationRouteRequest request,
            IWatchService watches,
            CancellationToken ct
        ) =>
        {
            var route = await watches.AddRouteAsync(id, request, ct);
            return route is null
                ? TypedResults.NotFound()
                : TypedResults.Created($"/api/watches/{id}/routes/{route.Id}", route);
        }
    )
    .WithName("AddWatchRoute");

api.MapDelete(
        "/watches/{watchId:guid}/routes/{routeId:guid}",
        async Task<Results<NoContent, NotFound>> (
            Guid watchId,
            Guid routeId,
            IWatchService watches,
            CancellationToken ct
        ) =>
            await watches.DeleteRouteAsync(watchId, routeId, ct)
                ? TypedResults.NoContent()
                : TypedResults.NotFound()
    )
    .WithName("DeleteWatchRoute");

api.MapGet(
        "/listings",
        async Task<Ok<IReadOnlyList<ListingResponse>>> (
            Guid? watchId,
            bool bookmarkedOnly,
            IListingQueryService listings,
            CancellationToken ct
        ) => TypedResults.Ok(await listings.GetForWatchAsync(watchId, bookmarkedOnly, ct))
    )
    .WithName("ListListings");

api.MapDelete(
        "/listings",
        async Task<Ok<DeleteAllListingsResponse>> (
            IListingQueryService listings,
            CancellationToken ct
        ) => TypedResults.Ok(new DeleteAllListingsResponse(await listings.DeleteAllAsync(ct)))
    )
    .WithName("DeleteAllListings")
    .WithSummary("Delete every listing (prices, sightings, and bookmarks cascade)");

api.MapGet(
        "/listings/{id:guid}",
        async Task<Results<Ok<ListingDetailResponse>, NotFound>> (
            Guid id,
            IListingQueryService listings,
            CancellationToken ct
        ) =>
        {
            var detail = await listings.GetDetailAsync(id, ct);
            return detail is null ? TypedResults.NotFound() : TypedResults.Ok(detail);
        }
    )
    .WithName("GetListing");

api.MapPost(
        "/listings/{id:guid}/bookmark",
        async Task<Results<NoContent, NotFound>> (
            Guid id,
            [FromQuery] bool value,
            IListingQueryService listings,
            CancellationToken ct
        ) =>
            await listings.SetBookmarkAsync(id, value, ct)
                ? TypedResults.NoContent()
                : TypedResults.NotFound()
    )
    .WithName("SetBookmark");

api.MapGet(
        "/scans",
        async Task<Ok<IReadOnlyList<ScanRunResponse>>> (
            IScanRunQueryService scans,
            CancellationToken ct
        ) => TypedResults.Ok(await scans.GetRecentAsync(100, ct))
    )
    .WithName("ListScans");

api.MapGet("/settings", (ISettingsService settings) => TypedResults.Ok(settings.GetSettings()))
    .WithName("GetSettings");

api.MapPost(
        "/culture",
        (string culture, HttpContext http) =>
        {
            if (culture is not ("sl-SI" or "en" or "sl" or "en-US"))
            {
                return Results.BadRequest();
            }

            var value = culture.StartsWith("sl", StringComparison.OrdinalIgnoreCase)
                ? "sl-SI"
                : "en";
            http.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(value)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );
            return Results.NoContent();
        }
    )
    .AllowAnonymous()
    .WithName("SetCulture");

api.MapPost(
        "/push/subscribe",
        async Task<NoContent> (
            PushSubscribeRequest request,
            IAppDbContext db,
            CancellationToken ct
        ) =>
        {
            var existing = await db.PushSubscriptions.FirstOrDefaultAsync(
                s => s.Endpoint == request.Endpoint,
                ct
            );
            if (existing is null)
            {
                db.PushSubscriptions.Add(
                    new Domolov.Domain.Entities.PushSubscriptionEntity
                    {
                        Endpoint = request.Endpoint,
                        P256dh = request.P256dh,
                        Auth = request.Auth,
                    }
                );
            }
            else
            {
                existing.P256dh = request.P256dh;
                existing.Auth = request.Auth;
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    )
    .WithName("PushSubscribe");

app.Run();

/// <summary>Web Push subscription payload.</summary>
public sealed record PushSubscribeRequest
{
    public required string Endpoint { get; init; }
    public required string P256dh { get; init; }
    public required string Auth { get; init; }
}

/// <summary>Marker for WebApplicationFactory.</summary>
public partial class Program;
