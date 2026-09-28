using Domolov.Application.Abstractions;
using Domolov.Infrastructure;
using Domolov.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Domolov.IntegrationTests;

/// <summary>One PostgreSQL container per test run; each test gets its own database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(
        "postgres:16-alpine"
    ).Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates an empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = "t_" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
        await cmd.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = name,
        }.ConnectionString;
    }

    /// <summary>A fully wired container (api role, fixture provider) over a fresh, migrated database.</summary>
    public async Task<TestApp> CreateAppAsync(
        Action<IServiceCollection>? configure = null,
        Dictionary<string, string?>? settings = null,
        bool migrate = true
    )
    {
        var connectionString = await CreateDatabaseAsync();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Domolov:Role"] = "Api",
            ["Domolov:AdminPassword"] = "test-password",
            ["Domolov:TimeZone"] = "Europe/Ljubljana",
            ["Domolov:FakeProvider"] = "true",
            ["Domolov:ScanCooldownMs"] = "0",
        };
        foreach (var (key, value) in settings ?? [])
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-01T08:00:00Z"));
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<TimeProvider>(clock);
        services.AddDomolovInfrastructure(configuration);
        services.RemoveAll<IHostedService>();
        var notifier = new RecordingNotifier();
        services.RemoveAll<INotifier>();
        services.AddSingleton<INotifier>(notifier);
        configure?.Invoke(services);

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        var app = new TestApp(provider, clock, notifier, connectionString);
        if (migrate)
        {
            await app.RunAsync<DatabaseMigrator>(m => m.MigrateAsync(CancellationToken.None));
        }

        return app;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>A service provider plus the knobs tests need.</summary>
public sealed class TestApp(
    ServiceProvider services,
    FakeTimeProvider clock,
    RecordingNotifier notifier,
    string connectionString
) : IAsyncDisposable
{
    public ServiceProvider Services { get; } = services;
    public FakeTimeProvider Clock { get; } = clock;
    public RecordingNotifier Notifier { get; } = notifier;
    public string ConnectionString { get; } = connectionString;

    /// <summary>Runs work in a fresh scope, like one request or one ScanRun.</summary>
    public async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> work)
        where TService : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task RunAsync<TService>(Func<TService, Task> work)
        where TService : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task<TResult> QueryAsync<TResult>(Func<DomolovDbContext, Task<TResult>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<DomolovDbContext>());
    }

    public ValueTask DisposeAsync() => Services.DisposeAsync();
}

/// <summary>Captures sent notifications instead of calling real services.</summary>
public sealed class RecordingNotifier : INotifier
{
    private readonly List<NotificationMessage> _sent = [];

    public Domain.Notifications.NotificationChannel Channel =>
        Domain.Notifications.NotificationChannel.Discord;

    public IReadOnlyList<NotificationMessage> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        lock (_sent)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_sent)
        {
            _sent.Clear();
        }
    }
}
