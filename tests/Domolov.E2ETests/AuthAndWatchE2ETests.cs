using System.Net;
using System.Net.Http.Json;
using Domolov.Application.Abstractions;
using Domolov.Application.Contracts;
using Domolov.Domain.Providers;
using Domolov.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Domolov.E2ETests;

public sealed class DomolovWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            RemoveDbRegistration(services);
            var dbName = "e2e-" + Guid.NewGuid();
            services.AddDbContext<DomolovDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DomolovDbContext>());

            services.RemoveAll<IListingProvider>();
            services.AddSingleton<IListingProvider, EmptyProvider>();
        });
    }

    private static void RemoveDbRegistration(IServiceCollection services)
    {
        var remove = services
            .Where(d =>
                d.ServiceType == typeof(DomolovDbContext)
                || d.ServiceType == typeof(IAppDbContext)
                || d.ServiceType == typeof(DbContextOptions)
                || d.ServiceType == typeof(DbContextOptions<DomolovDbContext>)
                || (
                    d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition()
                        == typeof(IDbContextOptionsConfiguration<>)
                )
            )
            .ToList();
        foreach (var d in remove)
        {
            services.Remove(d);
        }
    }

    private sealed class EmptyProvider : IListingProvider
    {
        public string Id => "nepremicnine";

        public bool CanHandle(Uri searchUrl) => true;

        public async IAsyncEnumerable<ListingCard> CrawlAsync(
            CrawlRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken
        )
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}

public sealed class AuthAndWatchE2ETests : IClassFixture<DomolovWebApplicationFactory>
{
    private readonly DomolovWebApplicationFactory _factory;

    public AuthAndWatchE2ETests(DomolovWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_and_create_watch_roundtrip()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var unauthorized = await client.GetAsync("/api/watches");
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Password = "changeme" }
        );
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await client.PostAsJsonAsync(
            "/api/watches",
            new CreateWatchRequest
            {
                Name = "E2E",
                SearchUrl = "https://www.nepremicnine.net/oglasi-prodaja/ljubljana/stanovanje/",
            }
        );
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var watch = await create.Content.ReadFromJsonAsync<WatchResponse>();
        watch.Should().NotBeNull();
        watch!.Name.Should().Be("E2E");

        var list = await client.GetFromJsonAsync<List<WatchResponse>>("/api/watches");
        list.Should().ContainSingle(w => w.Id == watch.Id);

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
