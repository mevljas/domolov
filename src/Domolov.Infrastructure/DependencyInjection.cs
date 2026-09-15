using Domolov.Application.Abstractions;
using Domolov.Application.Services;
using Domolov.Domain.Providers;
using Domolov.Domain.Services;
using Domolov.Infrastructure.Notifications;
using Domolov.Infrastructure.Persistence;
using Domolov.Infrastructure.Providers;
using Domolov.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Domolov.Infrastructure;

/// <summary>DI registration for infrastructure services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDomolovInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<DomolovOptions>(configuration.GetSection(DomolovOptions.SectionName));
        BindEnvOverrides(services, configuration);

        services.AddDbContext<DomolovDbContext>(options =>
        {
            var cs =
                configuration.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Database=domolov;Username=domolov;Password=domolov";
            options.UseNpgsql(cs);
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DomolovDbContext>());

        services.AddScoped<IWatchService, WatchService>();
        services.AddScoped<IListingQueryService, ListingQueryService>();
        services.AddScoped<IScanRunQueryService, ScanRunQueryService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IScanOrchestrator, ScanOrchestrator>();

        services.AddSingleton<PlaywrightBrowserHost>();
        services.AddSingleton<IListingProvider, NepremicnineProvider>();
        services.AddSingleton<IListingProviderResolver, ListingProviderResolver>();

        services.AddHttpClient<DiscordNotifier>();
        services.AddHttpClient<TelegramNotifier>();
        services.AddTransient<INotifier>(sp => sp.GetRequiredService<DiscordNotifier>());
        services.AddTransient<INotifier>(sp => sp.GetRequiredService<TelegramNotifier>());
        services.AddTransient<INotifier, EmailNotifier>();
        services.AddTransient<INotifier, WebPushNotifier>();

        services.AddHostedService<ScanSchedulerWorker>();

        return services;
    }

    private static void BindEnvOverrides(IServiceCollection services, IConfiguration configuration)
    {
        services.PostConfigure<DomolovOptions>(o =>
        {
            o.AdminPassword =
                configuration["DOMOLOV_ADMIN_PASSWORD"]
                ?? configuration["Domolov:AdminPassword"]
                ?? o.AdminPassword;
            o.TimeZone =
                configuration["DOMOLOV_TIMEZONE"]
                ?? configuration["Domolov:TimeZone"]
                ?? o.TimeZone;
            if (
                int.TryParse(
                    configuration["DOMOLOV_MAX_CONCURRENT_SCANS"]
                        ?? configuration["Domolov:MaxConcurrentScans"],
                    out var max
                )
            )
            {
                o.MaxConcurrentScans = max;
            }

            if (
                int.TryParse(
                    configuration["DOMOLOV_SCAN_COOLDOWN_MS"]
                        ?? configuration["Domolov:ScanCooldownMs"],
                    out var cooldown
                )
            )
            {
                o.ScanCooldownMs = Math.Max(0, cooldown);
            }

            if (
                bool.TryParse(
                    configuration["DOMOLOV_BROWSER_HEADLESS"]
                        ?? configuration["Domolov:BrowserHeadless"],
                    out var headless
                )
            )
            {
                o.BrowserHeadless = headless;
            }

            o.BrowserUserDataDir =
                configuration["DOMOLOV_BROWSER_USER_DATA_DIR"]
                ?? configuration["Domolov:BrowserUserDataDir"]
                ?? o.BrowserUserDataDir;
            o.Role = configuration["DOMOLOV_ROLE"] ?? configuration["Domolov:Role"] ?? o.Role;
            o.TelegramBotToken =
                configuration["DOMOLOV_TELEGRAM_BOT_TOKEN"]
                ?? configuration["Domolov:TelegramBotToken"]
                ?? o.TelegramBotToken;
            o.SmtpHost =
                configuration["DOMOLOV_SMTP_HOST"]
                ?? configuration["Domolov:SmtpHost"]
                ?? o.SmtpHost;
            if (
                int.TryParse(
                    configuration["DOMOLOV_SMTP_PORT"] ?? configuration["Domolov:SmtpPort"],
                    out var port
                )
            )
            {
                o.SmtpPort = port;
            }

            o.SmtpUser =
                configuration["DOMOLOV_SMTP_USER"]
                ?? configuration["Domolov:SmtpUser"]
                ?? o.SmtpUser;
            o.SmtpPassword =
                configuration["DOMOLOV_SMTP_PASSWORD"]
                ?? configuration["Domolov:SmtpPassword"]
                ?? o.SmtpPassword;
            o.SmtpFrom =
                configuration["DOMOLOV_SMTP_FROM"]
                ?? configuration["Domolov:SmtpFrom"]
                ?? o.SmtpFrom;
            o.VapidPublicKey =
                configuration["DOMOLOV_VAPID_PUBLIC_KEY"]
                ?? configuration["Domolov:VapidPublicKey"]
                ?? o.VapidPublicKey;
            o.VapidPrivateKey =
                configuration["DOMOLOV_VAPID_PRIVATE_KEY"]
                ?? configuration["Domolov:VapidPrivateKey"]
                ?? o.VapidPrivateKey;
            o.VapidSubject =
                configuration["DOMOLOV_VAPID_SUBJECT"]
                ?? configuration["Domolov:VapidSubject"]
                ?? o.VapidSubject;
        });
    }
}
