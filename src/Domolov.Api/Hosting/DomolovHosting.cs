using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Domolov.Api.Auth;
using Domolov.Api.Endpoints;
using Domolov.Api.OpenApi;
using Domolov.Application.Common;
using Domolov.Application.Options;
using Domolov.Infrastructure;
using Domolov.Infrastructure.Persistence;
using Domolov.Infrastructure.Workers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

namespace Domolov.Api.Hosting;

/// <summary>Service registration and pipeline for the Domolov host.</summary>
public static class DomolovHosting
{
    public const string LoginRateLimitPolicy = "login";

    /// <summary>True while <c>dotnet build</c> exports the OpenAPI document.</summary>
    public static bool IsOpenApiGeneration =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    /// <summary>Keeps build-time OpenAPI export away from the database and the worker.</summary>
    public static readonly Dictionary<string, string?> OpenApiGenerationSettings = new()
    {
        ["Domolov:Role"] = "Api",
        ["Domolov:AdminPassword"] = "openapi-generation",
    };

    public static void AddDomolovLogging(this WebApplicationBuilder builder) =>
        builder.Host.UseSerilog(
            (ctx, cfg) =>
            {
                var level = Enum.TryParse<LogEventLevel>(
                    ctx.Configuration["DOMOLOV_LOG_LEVEL"],
                    true,
                    out var parsed
                )
                    ? parsed
                    : LogEventLevel.Information;
                cfg.MinimumLevel.Is(level)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console();
                var file = ctx.Configuration["DOMOLOV_LOG_FILE"];
                if (!string.IsNullOrWhiteSpace(file))
                {
                    cfg.WriteTo.File(
                        file,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        fileSizeLimitBytes: 50L * 1024 * 1024,
                        rollOnFileSizeLimit: true
                    );
                }
            }
        );

    public static IServiceCollection AddDomolovApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            );
            o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });
        services.AddProblemDetails(o =>
            o.CustomizeProblemDetails = ctx =>
                ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier
        );
        services.AddExceptionHandler<DomolovExceptionHandler>();
        services.AddValidation();
        services.AddDomolovOpenApi();

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
            o.ForwardLimit = 2;
        });

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(SessionCookie.Configure);
        services.AddAuthorization();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(
                LoginRateLimitPolicy,
                http =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                        }
                    )
            );
            o.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                {
                    ctx.HttpContext.Response.Headers.RetryAfter = (
                        (int)retry.TotalSeconds
                    ).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await Results
                    .Problem(
                        title: "Too many sign-in attempts",
                        detail: "Wait a minute and try again.",
                        statusCode: StatusCodes.Status429TooManyRequests
                    )
                    .ExecuteAsync(ctx.HttpContext);
            };
        });

        var health = services
            .AddHealthChecks()
            .AddDbContextCheck<DomolovDbContext>("database", tags: ["ready"]);
        if (DependencyInjection.ReadRole(configuration) is DomolovRole.All or DomolovRole.Worker)
        {
            health.AddCheck<BrowserHealthCheck>("browser", tags: ["ready"]);
        }

        var otel = services
            .AddOpenTelemetry()
            .ConfigureResource(r =>
                r.AddService(
                    "domolov",
                    serviceVersion: typeof(DomolovHosting).Assembly.GetName().Version?.ToString()
                )
            )
            .WithTracing(t =>
                t.AddAspNetCoreInstrumentation(o =>
                        o.Filter = http => !http.Request.Path.StartsWithSegments("/health")
                    )
                    .AddHttpClientInstrumentation(o => o.RecordException = true)
                    .AddSource(DomolovTelemetry.Name, "Domolov.Notifications")
            )
            .WithMetrics(m =>
                m.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(DomolovTelemetry.Name)
            );
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.WithTracing(t => t.AddOtlpExporter()).WithMetrics(m => m.AddOtlpExporter());
        }

        return services;
    }

    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<DatabaseMigrator>()
            .MigrateAsync(CancellationToken.None);
    }

    public static WebApplication UseDomolovApi(this WebApplication app)
    {
        var role = DependencyInjection.ReadRole(app.Configuration);
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSerilogRequestLogging(o =>
            o.GetLevel = (http, _, ex) =>
                ex is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
                : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information
        );
        app.UseMiddleware<EnumQueryCasing>();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();
        app.MapHealthChecks(
                "/health/ready",
                new HealthCheckOptions
                {
                    Predicate = c => c.Tags.Contains("ready"),
                    ResultStatusCodes =
                    {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status200OK,
                        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
                    },
                }
            )
            .AllowAnonymous();

        if (role is DomolovRole.Worker)
        {
            return app;
        }

        var openApi = app.MapOpenApi("/api/openapi/{documentName}.json");
        var docs = app.MapScalarApiReference(
            "/api/docs",
            o =>
                o.WithTitle("Domolov API")
                    .WithOpenApiRoutePattern("/api/openapi/{documentName}.json")
                    .WithTheme(ScalarTheme.Kepler)
                    .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl)
        );
        if (!app.Environment.IsDevelopment())
        {
            openApi.RequireAuthorization();
            docs.RequireAuthorization();
        }

        app.MapDomolovEndpoints();
        return app;
    }
}
