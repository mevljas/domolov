using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domolov.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Domolov.ApiTests;

/// <summary>The real API host (api role, fixture provider) over a Testcontainers PostgreSQL.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "api-test-password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:16-alpine"
    ).Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Migrate before the host starts: Data Protection loads its key ring from the database
        // at startup, exactly like the `migrate` step that runs before api/worker in production.
        var options = new DbContextOptionsBuilder<DomolovDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = new DomolovDbContext(options);
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Domolov:Role", "Api");
        builder.UseSetting("Domolov:AdminPassword", Password);
        builder.UseSetting("Domolov:FakeProvider", "true");
        builder.UseSetting("Domolov:TimeZone", "Europe/Ljubljana");
    }

    /// <summary>A client that sends the CSRF header and comes from its own IP (own rate-limit bucket).</summary>
    public HttpClient Client(bool csrfHeader = true)
    {
        var client = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        client.DefaultRequestHeaders.Add(
            "X-Forwarded-For",
            $"10.0.{Random.Shared.Next(255)}.{Random.Shared.Next(1, 255)}"
        );
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
        if (csrfHeader)
        {
            client.DefaultRequestHeaders.Add("X-Requested-With", "domolov");
        }

        return client;
    }

    public async Task<HttpClient> SignedInClientAsync()
    {
        var client = Client();
        var response = await client.PostAsJsonAsync("/api/session", new { password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
