using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
        builder.UseSetting(
            "DOMOLOV_DATA_PROTECTION_KEYS_DIR",
            Path.Combine(Path.GetTempPath(), "domolov-dp-keys-e2e-" + Guid.NewGuid().ToString("N"))
        );
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
    private static readonly Regex AntiforgeryTokenRegex = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

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

    [Fact]
    public async Task Login_page_emits_antiforgery_form_posting_to_auth_login()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var loginPage = await client.GetAsync("/login");
        loginPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await loginPage.Content.ReadAsStringAsync();

        html.Should()
            .Contain("__RequestVerificationToken", "login form must emit an antiforgery token");
        html.Should()
            .Contain(
                "action=\"/auth/login\"",
                "login form must POST to /auth/login (not Blazor EditForm to /login)"
            );
        html.Should().Contain("data-enhance=\"false\"");
    }

    [Fact]
    public async Task Cookie_login_without_antiforgery_is_rejected()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["password"] = "changeme" }
        );

        var response = await client.PostAsync("/auth/login", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cookie_login_with_antiforgery_signs_in_and_redirects_home()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var anonymousHome = await client.GetAsync("/");
        anonymousHome.StatusCode.Should().Be(HttpStatusCode.Redirect);
        anonymousHome.Headers.Location?.ToString().Should().Contain("/login");

        var loginPost = await PostCookieLoginAsync(client, "changeme");
        loginPost.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginPost.Headers.Location?.ToString().Should().Be("/");

        var setCookie = loginPost
            .Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.StartsWith("domolov_auth=", StringComparison.Ordinal));
        setCookie.Should().NotBeNullOrEmpty("login must set the domolov_auth cookie");
        setCookie!
            .ToLowerInvariant()
            .Should()
            .Match(
                c => c.Contains("expires=") || c.Contains("max-age="),
                "auth cookie must be persistent"
            );

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);

        var watches = await client.GetAsync("/api/watches");
        watches.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cookie_login_with_wrong_password_redirects_back_to_login()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var loginPost = await PostCookieLoginAsync(client, "not-the-password");
        loginPost.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginPost.Headers.Location?.ToString().Should().Be("/login?error=true");

        var errorPage = await client.GetAsync("/login?error=true");
        errorPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await errorPage.Content.ReadAsStringAsync();
        html.Should().Contain("text-danger");

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.Redirect);
        home.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task Nav_shows_sign_in_when_anonymous_and_sign_out_when_authenticated()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var loginPage = await client.GetAsync("/login");
        loginPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var anonymousHtml = await loginPage.Content.ReadAsStringAsync();
        anonymousHtml.Should().Contain("href=\"login\"");
        anonymousHtml.Should().NotContain("action=\"/auth/logout\"");

        var loginPost = await PostCookieLoginAsync(client, "changeme");
        loginPost.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);
        var authenticatedHtml = await home.Content.ReadAsStringAsync();
        authenticatedHtml.Should().Contain("action=\"/auth/logout\"");
        authenticatedHtml.Should().Contain("data-enhance=\"false\"");
        authenticatedHtml
            .Should()
            .Contain("__RequestVerificationToken", "logout form must emit an antiforgery token");
        authenticatedHtml.Should().NotContain("href=\"login\"");
    }

    [Fact]
    public async Task Cookie_logout_clears_auth_and_redirects_to_login()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var loginPost = await PostCookieLoginAsync(client, "changeme");
        loginPost.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await home.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenRegex.Match(html).Groups[1].Value;
        token.Should().NotBeNullOrWhiteSpace();

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }
        );
        var logout = await client.PostAsync("/auth/logout", content);
        logout.StatusCode.Should().Be(HttpStatusCode.Redirect);
        logout.Headers.Location?.ToString().Should().Be("/login");

        var afterLogout = await client.GetAsync("/");
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Redirect);
        afterLogout.Headers.Location?.ToString().Should().Contain("/login");

        var loginAgain = await client.GetAsync("/login");
        loginAgain.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginHtml = await loginAgain.Content.ReadAsStringAsync();
        loginHtml.Should().Contain("href=\"login\"");
        loginHtml.Should().NotContain("action=\"/auth/logout\"");
    }

    [Fact]
    public async Task Login_page_redirects_home_when_already_authenticated()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var loginPost = await PostCookieLoginAsync(client, "changeme");
        loginPost.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var loginPage = await client.GetAsync("/login");
        loginPage.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginPage.Headers.Location?.ToString().Should().BeOneOf("/", "http://localhost/");
    }

    private static async Task<HttpResponseMessage> PostCookieLoginAsync(
        HttpClient client,
        string password
    )
    {
        var loginPage = await client.GetAsync("/login");
        loginPage.EnsureSuccessStatusCode();
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenRegex.Match(html).Groups[1].Value;
        token.Should().NotBeNullOrWhiteSpace();

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["password"] = password,
            }
        );

        return await client.PostAsync("/auth/login", content);
    }
}
