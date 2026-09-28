using Domolov.Domain.Homes;
using Domolov.Domain.Providers;
using Domolov.Infrastructure.Scanning;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using SkiaSharp;

namespace Domolov.UnitTests.Infrastructure;

public sealed class ImageHasherTests
{
    private static byte[] Render(
        int width,
        int height,
        SKColor sky,
        SKColor house,
        SKEncodedImageFormat format,
        int quality
    )
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(sky);
        using var paint = new SKPaint { Color = house, IsAntialias = true };
        canvas.DrawRect(width * 0.3f, height * 0.4f, width * 0.4f, height * 0.5f, paint);
        using var roof = new SKPaint { Color = SKColors.DarkRed };
        canvas.DrawRect(width * 0.25f, height * 0.25f, width * 0.5f, height * 0.15f, roof);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, quality).ToArray();
    }

    [Fact]
    public void Same_photo_recompressed_and_resized_keeps_a_close_hash()
    {
        var original = ImageHasher.Compute(
            Render(800, 600, SKColors.LightSkyBlue, SKColors.Beige, SKEncodedImageFormat.Png, 100)
        );
        var smallJpeg = ImageHasher.Compute(
            Render(400, 300, SKColors.LightSkyBlue, SKColors.Beige, SKEncodedImageFormat.Jpeg, 55)
        );

        PerceptualHash
            .Distance(original!.Value, smallJpeg!.Value)
            .Should()
            .BeLessThanOrEqualTo(HomeMatchScorer.PhotoIdenticalDistance);
    }

    [Fact]
    public void Different_photo_is_far_away()
    {
        var a = ImageHasher
            .Compute(
                Render(
                    800,
                    600,
                    SKColors.LightSkyBlue,
                    SKColors.Beige,
                    SKEncodedImageFormat.Png,
                    100
                )
            )!
            .Value;
        using var bitmap = new SKBitmap(800, 600);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var gradient = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(800, 0),
                [SKColors.Black, SKColors.White],
                SKShaderTileMode.Clamp
            );
            using var paint = new SKPaint { Shader = gradient };
            canvas.DrawRect(0, 0, 800, 600, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        var b = ImageHasher.Compute(image.Encode(SKEncodedImageFormat.Png, 100).ToArray())!.Value;

        PerceptualHash
            .Distance(a, b)
            .Should()
            .BeGreaterThan(HomeMatchScorer.PhotoIdenticalDistance);
    }

    [Fact]
    public void Garbage_bytes_yield_no_hash()
    {
        ImageHasher.Compute([1, 2, 3, 4]).Should().BeNull();
        ImageHasher.Compute([]).Should().BeNull();
    }
}

public sealed class HumanPacerTests
{
    private readonly HumanPacer _pacer = new(new Random(1234), new FakeTimeProvider());

    [Fact]
    public void Dwell_time_is_bounded()
    {
        for (var i = 0; i < 1000; i++)
        {
            _pacer
                .DwellTime()
                .Should()
                .BeGreaterThanOrEqualTo(HumanPacer.MinDwell)
                .And.BeLessThanOrEqualTo(HumanPacer.MaxDwell);
        }
    }

    [Fact]
    public void Scroll_plan_covers_the_distance_with_variable_steps_and_some_scroll_backs()
    {
        var plan = _pacer.ScrollPlan(6000);

        plan.Sum().Should().BeGreaterThanOrEqualTo(6000);
        plan.Where(s => s > 0).Should().OnlyContain(s => s >= 80 && s <= 400);
        plan.Distinct().Count().Should().BeGreaterThan(5);
        plan.Should().Contain(s => s < 0);
    }

    [Fact]
    public void Short_pauses_and_mouse_targets_stay_in_range()
    {
        for (var i = 0; i < 200; i++)
        {
            _pacer.ShortPause().TotalMilliseconds.Should().BeInRange(120, 900);
            var (x, y, steps) = _pacer.MouseTarget(1366, 768);
            x.Should().BeInRange(0, 1366);
            y.Should().BeInRange(0, 768);
            steps.Should().BeInRange(8, 25);
        }
    }

    [Fact]
    public void Cooldown_jitter_is_within_a_quarter()
    {
        for (var i = 0; i < 200; i++)
        {
            _pacer.Jitter(TimeSpan.FromSeconds(20)).TotalSeconds.Should().BeInRange(15, 25);
        }

        _pacer.Jitter(TimeSpan.Zero).Should().Be(TimeSpan.Zero);
    }
}

public sealed class UserAgentBuilderTests
{
    [Theory]
    [InlineData("Chrome/140.0.7339.16", "140.0.7339.16")]
    [InlineData("HeadlessChrome/141.0.7390.37", "141.0.7390.37")]
    [InlineData("Firefox/1.0", null)]
    public void Parses_the_browser_version(string product, string? expected) =>
        UserAgentBuilder.ParseVersion(product).Should().Be(expected);

    [Fact]
    public void Ua_and_client_hints_agree_and_never_say_headless()
    {
        var args = UserAgentBuilder.OverrideArguments("140.0.7339.16");
        var ua = (string)args["userAgent"];
        var metadata = (Dictionary<string, object>)args["userAgentMetadata"];
        var brands = (Dictionary<string, string>[])metadata["brands"];

        ua.Should()
            .Contain("Chrome/140.0.0.0")
            .And.Contain("X11; Linux x86_64")
            .And.NotContain("Headless");
        brands.Should().Contain(b => b["brand"] == "Google Chrome" && b["version"] == "140");
        brands.Should().NotContain(b => b["brand"].Contains("Headless"));
        metadata["fullVersion"].Should().Be("140.0.7339.16");
        metadata["platform"].Should().Be("Linux");
        args["acceptLanguage"].Should().Be(BrowserFingerprint.AcceptLanguage);
    }
}

public sealed class BrowserFingerprintStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "domolov-fp-" + Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void Fingerprint_is_created_once_and_reused()
    {
        var first = BrowserFingerprintStore.LoadOrCreate(_dir, new Random(1), TestData.Now);
        var second = BrowserFingerprintStore.LoadOrCreate(
            _dir,
            new Random(999),
            TestData.Now.AddDays(1)
        );

        second.Should().Be(first);
        first.ViewportWidth.Should().Be(first.ScreenWidth);
        first.ViewportHeight.Should().BeLessThan(first.ScreenHeight);
    }

    [Fact]
    public void Corrupt_file_is_regenerated()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, BrowserFingerprintStore.FileName), "{not json");

        BrowserFingerprintStore
            .LoadOrCreate(_dir, new Random(1), TestData.Now)
            .ScreenWidth.Should()
            .BeGreaterThan(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}

public sealed class FixtureListingProviderTests
{
    [Fact]
    public void Rounds_evolve_the_market()
    {
        var round1 = FixtureListingProvider.CardsForRound(1);
        var round2 = FixtureListingProvider.CardsForRound(2);
        var round4 = FixtureListingProvider.CardsForRound(4);

        round1.Should().Contain(c => c.ExternalId == "6411005");
        round2.Should().NotContain(c => c.ExternalId == "6411005");
        round2.Should().Contain(c => c.ExternalId == "6412109");
        round4.Should().Contain(c => c.ExternalId == "6412105");
        round2
            .Single(c => c.ExternalId == "6411003")
            .Price.Should()
            .BeLessThan(round1.Single(c => c.ExternalId == "6411003").Price!.Value);
        round1.Select(c => c.PageIndex).Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Each_crawl_of_a_url_advances_a_round_and_reaches_the_end()
    {
        var provider = new FixtureListingProvider();
        var url = new Uri($"https://{FixtureListingProvider.Host}/oglasi-prodaja/ljubljana/");
        provider.CanHandle(url).Should().BeTrue();

        var first = new CrawlRequest(url, Guid.NewGuid());
        var cards1 = await provider.CrawlAsync(first, CancellationToken.None).ToListAsync();
        var second = new CrawlRequest(url, Guid.NewGuid());
        var cards2 = await provider.CrawlAsync(second, CancellationToken.None).ToListAsync();

        first.Progress.ReachedEnd.Should().BeTrue();
        var seen = FixtureListingProvider.ScopedExternalId(url, "6411005");
        cards1.Should().Contain(c => c.ExternalId == seen);
        cards2.Should().NotContain(c => c.ExternalId == seen);
    }

    [Fact]
    public void Demo_photos_are_valid_svg_and_hashes_are_stable()
    {
        DemoPhotos.Svg("p01").Should().StartWith("<svg").And.Contain("</svg>");
        FixtureListingProvider
            .PhotoHash("p05")
            .Should()
            .Be(FixtureListingProvider.PhotoHash("p05"));
        PerceptualHash
            .Distance(
                FixtureListingProvider.PhotoHash("p01"),
                FixtureListingProvider.PhotoHash("p02")
            )
            .Should()
            .BeGreaterThan(HomeMatchScorer.PhotoUnrelatedDistance);
    }
}
