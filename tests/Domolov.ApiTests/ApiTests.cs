using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Domolov.ApiTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Api_returns_401_problem_instead_of_redirecting()
    {
        var response = await factory.Client().GetAsync("/api/watches");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Sign_in_sets_an_http_only_strict_cookie_and_opens_a_session()
    {
        var client = factory.Client();

        var login = await client.PostAsJsonAsync(
            "/api/session",
            new { password = ApiFactory.Password }
        );

        login.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cookie = login
            .Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith("domolov_session=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly").And.Contain("samesite=strict");
        var session = await client.GetFromJsonAsync<JsonElement>("/api/session");
        session.GetProperty("name").GetString().Should().Be("admin");
    }

    [Fact]
    public async Task Wrong_password_is_a_401_problem()
    {
        var response = await factory
            .Client()
            .PostAsJsonAsync("/api/session", new { password = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("title")
            .GetString()
            .Should()
            .Be("Wrong password");
    }

    [Fact]
    public async Task State_changing_requests_need_the_csrf_header()
    {
        var response = await factory
            .Client(csrfHeader: false)
            .PostAsJsonAsync("/api/session", new { password = ApiFactory.Password });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Repeated_failures_are_rate_limited()
    {
        var client = factory.Client();
        HttpResponseMessage last = null!;
        for (var i = 0; i < 6; i++)
        {
            last = await client.PostAsJsonAsync("/api/session", new { password = "guess" + i });
        }

        last.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        last.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Sign_out_everywhere_revokes_the_session()
    {
        var client = await factory.SignedInClientAsync();
        var other = await factory.SignedInClientAsync();

        (await client.DeleteAsync("/api/sessions"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        (await other.GetAsync("/api/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_docs_and_openapi_require_a_session_outside_development()
    {
        var anonymous = factory.Client();
        (await anonymous.GetAsync("/api/openapi/v1.json"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/docs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var client = await factory.SignedInClientAsync();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/openapi/v1.json");
        doc.GetProperty("info").GetProperty("title").GetString().Should().Be("Domolov API");
        var docs = await client.GetAsync("/api/docs");
        if (docs.StatusCode == HttpStatusCode.Found)
        {
            docs = await client.GetAsync(
                new Uri(docs.RequestMessage!.RequestUri!, docs.Headers.Location!)
            );
        }

        docs.StatusCode.Should().Be(HttpStatusCode.OK, docs.RequestMessage!.RequestUri!.ToString());
        (await docs.Content.ReadAsStringAsync()).Should().Contain("Domolov API");
    }

    [Fact]
    public async Task Health_endpoints_are_public()
    {
        var client = factory.Client();
        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection(ApiCollection.Name)]
public sealed class WatchApiTests(ApiFactory factory)
{
    private static object NewWatch(string suffix) =>
        new
        {
            name = "Ljubljana " + suffix,
            searchUrl = $"https://fixtures.domolov.test/oglasi-prodaja/{suffix}/",
            cron = "0 */6 * * *",
            initialRoute = new
            {
                channel = "discord",
                destination = "https://discord.com/api/webhooks/1/abc",
                triggers = new[] { "newListing", "reposted" },
            },
        };

    [Fact]
    public async Task Create_returns_201_with_location_and_etag_protects_updates()
    {
        var client = await factory.SignedInClientAsync();

        var created = await client.PostAsJsonAsync(
            "/api/watches",
            NewWatch(Guid.NewGuid().ToString("N")[..8])
        );
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = created.Headers.Location!.ToString();
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("routes")[0]
            .GetProperty("triggers")
            .EnumerateArray()
            .Select(t => t.GetString())
            .Should()
            .Equal("newListing", "reposted");

        var get = await client.GetAsync(location);
        var etag = get.Headers.ETag!.ToString();

        var stale = new HttpRequestMessage(HttpMethod.Patch, location)
        {
            Content = JsonContent.Create(new { isPaused = true }),
        };
        stale.Headers.TryAddWithoutValidation("If-Match", "W/\"1\"");
        (await client.SendAsync(stale)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var fresh = new HttpRequestMessage(HttpMethod.Patch, location)
        {
            Content = JsonContent.Create(new { isPaused = true, name = "Renamed" }),
        };
        fresh.Headers.TryAddWithoutValidation("If-Match", etag);
        var patched = await client.SendAsync(fresh);
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        var watch = await patched.Content.ReadFromJsonAsync<JsonElement>();
        watch.GetProperty("isPaused").GetBoolean().Should().BeTrue();
        watch.GetProperty("name").GetString().Should().Be("Renamed");
        watch.GetProperty("nextRunAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Invalid_input_returns_a_validation_problem_per_field()
    {
        var client = await factory.SignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/watches",
            new
            {
                name = "x",
                searchUrl = "https://example.com/search",
                cron = "0 * * * *",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem
            .GetProperty("errors")
            .GetProperty("searchUrl")[0]
            .GetString()
            .Should()
            .Contain("not supported");
    }

    [Fact]
    public async Task Run_now_returns_202_pointing_at_the_scan()
    {
        var client = await factory.SignedInClientAsync();
        var created = await (
            await client.PostAsJsonAsync(
                "/api/watches",
                NewWatch(Guid.NewGuid().ToString("N")[..8])
            )
        ).Content.ReadFromJsonAsync<JsonElement>();

        var run = await client.PostAsync(
            $"/api/watches/{created.GetProperty("id").GetGuid()}/scans",
            null
        );

        run.StatusCode.Should().Be(HttpStatusCode.Accepted);
        run.Headers.Location!.ToString().Should().StartWith("/api/scans/");
        (await client.GetAsync(run.Headers.Location)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_watch_is_a_404_problem()
    {
        var client = await factory.SignedInClientAsync();
        var response = await client.GetAsync($"/api/watches/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Search_url_check_suggests_a_name()
    {
        var client = await factory.SignedInClientAsync();
        var result = await (
            await client.PostAsJsonAsync(
                "/api/search-url-checks",
                new
                {
                    url = "https://www.nepremicnine.net/oglasi-prodaja/ljubljana-mesto/stanovanje/",
                }
            )
        ).Content.ReadFromJsonAsync<JsonElement>();

        result.GetProperty("supported").GetBoolean().Should().BeTrue();
        result.GetProperty("suggestedName").GetString().Should().Be("Stanovanje · Ljubljana mesto");
    }

    [Fact]
    public async Task Homes_feed_returns_a_page_envelope()
    {
        var client = await factory.SignedInClientAsync();
        var response = await client.GetAsync(
            "/api/homes?sort=priceAsc&page=1&pageSize=5&status=all"
        );
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        page.GetProperty("page").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(5);
        page.TryGetProperty("total", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Scan_events_stream_starts_with_a_ready_event()
    {
        var client = await factory.SignedInClientAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await client.GetAsync(
            "/api/scans/events",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token
        );

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
        var first = await reader.ReadLineAsync(cts.Token);
        first.Should().Be("event: ready");
    }

    [Fact]
    public async Task Demo_photos_are_served_when_the_fixture_provider_is_on()
    {
        var response = await factory.Client().GetAsync("/api/demo/photos/p01.svg");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
    }
}
