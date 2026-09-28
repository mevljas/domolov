using Domolov.Application;
using Domolov.Application.Abstractions;
using Domolov.Application.Options;
using Domolov.Domain.Providers;
using Domolov.Infrastructure.Notifications;
using Domolov.Infrastructure.Persistence;
using Domolov.Infrastructure.Scanning;
using Domolov.Infrastructure.Signals;
using Domolov.Infrastructure.Workers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Domolov.Infrastructure;

/// <summary>DI registration for infrastructure services.</summary>
public static class DependencyInjection
{
    public const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=domolov;Username=domolov;Password=domolov";

    /// <summary>Reads the role before the container is built (decides which services exist).</summary>
    public static DomolovRole ReadRole(IConfiguration configuration) =>
        configuration
            .GetSection(DomolovOptions.SectionName)
            .GetValue<DomolovRole?>(nameof(DomolovOptions.Role)) ?? DomolovRole.All;

    public static IServiceCollection AddDomolovInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<DomolovOptions>()
            .Bind(configuration.GetSection(DomolovOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                o => IsValidTimeZone(o.TimeZone),
                "DOMOLOV_TIMEZONE must be an IANA timezone id."
            )
            .Validate(
                o =>
                    !o.RunsApi
                    || !string.IsNullOrEmpty(o.AdminPassword)
                    || !string.IsNullOrEmpty(o.AdminPasswordHash),
                "Set DOMOLOV_ADMIN_PASSWORD or DOMOLOV_ADMIN_PASSWORD_HASH."
            )
            .ValidateOnStart();
        services
            .AddOptions<RetentionOptions>()
            .Bind(configuration.GetSection(RetentionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services
            .AddOptions<MatchOptions>()
            .Bind(configuration.GetSection(MatchOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                m => m.PossibleScore <= m.AutoLinkScore,
                "DOMOLOV_MATCH_POSSIBLE_SCORE must not exceed the auto-link score."
            )
            .ValidateOnStart();

        var connectionString = WithoutGssProbe(
            configuration.GetConnectionString("Default") ?? DefaultConnectionString
        );
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<DomolovDbContext>(
            (sp, options) => options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())
        );
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DomolovDbContext>());
        services.AddScoped<DatabaseMigrator>();
        services
            .AddDataProtection()
            .SetApplicationName("Domolov")
            .PersistKeysToDbContext<DomolovDbContext>();

        services.AddDomolovApplication();
        services.TryAddSingleton<ISignalPublisher, PostgresSignalPublisher>();
        services.TryAddSingleton<SignalBus>();
        services.TryAddSingleton<IScanRunEventHub, ScanRunEventHub>();
        services.TryAddSingleton<IStorageStats, PostgresStorageStats>();
        services.AddHostedService<PostgresSignalListener>();

        services.AddSingleton(sp => new HumanPacer(
            Random.Shared,
            sp.GetRequiredService<TimeProvider>()
        ));
        services.AddSingleton<PlaywrightBrowserHost>();
        services.AddSingleton<BrowserCheck>();
        services.AddSingleton<IListingProvider, NepremicnineProvider>();
        if (
            configuration
                .GetSection(DomolovOptions.SectionName)
                .GetValue<bool>(nameof(DomolovOptions.FakeProvider))
        )
        {
            services.AddSingleton<IListingProvider, FixtureListingProvider>();
        }

        services.AddSingleton<IListingProviderResolver, ListingProviderResolver>();

        services.AddHttpClient<DiscordNotifier>();
        services.AddHttpClient<TelegramNotifier>();
        services.AddTransient<INotifier>(sp => sp.GetRequiredService<DiscordNotifier>());
        services.AddTransient<INotifier>(sp => sp.GetRequiredService<TelegramNotifier>());
        services.AddTransient<INotifier, EmailNotifier>();
        services.AddTransient<INotifier, WebPushNotifier>();

        if (ReadRole(configuration) is DomolovRole.All or DomolovRole.Worker)
        {
            services.AddHostedService<ScanSchedulerService>();
            services.AddHostedService<ScanQueueService>();
            services.AddHostedService<CleanupService>();
        }

        return services;
    }

    private static bool IsValidTimeZone(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    /// <summary>
    /// Npgsql probes for Kerberos (GSS) encryption on every connection and logs an error when
    /// libgssapi is absent, as in the slim images. Disable the probe unless explicitly configured.
    /// </summary>
    private static string WithoutGssProbe(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!connectionString.Contains("GSS", StringComparison.OrdinalIgnoreCase))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }
}
