using System.Reflection;
using Domolov.Application.Homes;
using Domolov.Application.Notifications;
using Domolov.Application.Retention;
using Domolov.Application.Scans;
using Domolov.Application.Watches;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Domolov.Application;

/// <summary>Registers application services and every use-case handler.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDomolovApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        foreach (
            var type in Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    t is { IsClass: true, IsAbstract: false, IsPublic: true }
                    && t.Name.EndsWith("Handler", StringComparison.Ordinal)
                )
        )
        {
            services.TryAddScoped(type);
        }

        services.TryAddScoped<WatchReader>();
        services.TryAddScoped<ScanQueue>();
        services.TryAddScoped<ScanExecutor>();
        services.TryAddScoped<HomeLinker>();
        services.TryAddScoped<HomeMatcher>();
        services.TryAddScoped<NotificationDispatcher>();
        services.TryAddScoped<RetentionCleaner>();
        services.TryAddScoped<Auth.SessionValidator>();
        return services;
    }
}
