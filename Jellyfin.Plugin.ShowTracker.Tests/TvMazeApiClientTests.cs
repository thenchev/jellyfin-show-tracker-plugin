using System.Net;
using FluentAssertions;
using Jellyfin.Plugin.ShowTracker.Api;
using Jellyfin.Plugin.ShowTracker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ShowTracker.Tests;

public class TvMazeApiClientTests : IDisposable
{
    private readonly string _tempDir;

    public TvMazeApiClientTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "showtracker_api_tests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private (TvMazeApiClient client, StubHttpMessageHandler handler) BuildClient()
    {
        var handler = new StubHttpMessageHandler();
        var cache = new TvMazeCache(NullLogger<TvMazeCache>.Instance, _tempDir, () => 12);
        var limiter = new TvMazeRateLimiter(NullLogger<TvMazeRateLimiter>.Instance);
        var client = new TvMazeApiClient(limiter, cache, NullLogger<TvMazeApiClient>.Instance, handler);
        return (client, handler);
    }

    [Fact]
    public async Task LookupByTvdb_ParsesShowResponse()
    {
        var (client, handler) = BuildClient();
        handler.Enqueue(HttpStatusCode.OK, """
            { "id": 82, "name": "Game of Thrones", "status": "Ended",
              "url": "https://www.tvmaze.com/shows/82/game-of-thrones",
              "image": { "medium": "http://example/m.jpg", "original": "http://example/o.jpg" } }
            """);

        var show = await client.LookupByTvdbAsync("121361");

        show.Should().NotBeNull();
        show!.Id.Should().Be(82);
        show.Name.Should().Be("Game of Thrones");
        show.Status.Should().Be("Ended");

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/lookup/shows?thetvdb=121361");
    }

    [Fact]
    public async Task LookupByTvdb_ReturnsNull_OnNotFound()
    {
        var (client, handler) = BuildClient();
        handler.Enqueue(HttpStatusCode.NotFound);

        var show = await client.LookupByTvdbAsync("999999");

        show.Should().BeNull();
    }

    [Fact]
    public async Task LookupByTvdb_CachesSuccessfulResponse()
    {
        var (client, handler) = BuildClient();
        handler.Enqueue(HttpStatusCode.OK, """{ "id": 1, "name": "X", "url": "u" }""");

        await client.LookupByTvdbAsync("1");
        await client.LookupByTvdbAsync("1"); // second call should hit the cache

        handler.CallCount.Should().Be(1, "the second lookup must be served from cache");
    }

    [Fact]
    public async Task GetAsync_RetriesOn429_ThenSucceeds()
    {
        var (client, handler) = BuildClient();
        handler.Enqueue((HttpStatusCode)429);
        handler.Enqueue(HttpStatusCode.OK, "[]");

        var episodes = await client.GetEpisodesAsync(42);

        episodes.Should().NotBeNull().And.BeEmpty();
        handler.CallCount.Should().Be(2, "the client must retry once after a 429");
    }

    [Fact]
    public async Task GetEpisodes_ParsesArray()
    {
        var (client, handler) = BuildClient();
        handler.Enqueue(HttpStatusCode.OK, """
            [
              { "id": 1, "name": "Pilot", "season": 1, "number": 1, "airdate": "2020-01-01", "url": "u1" },
              { "id": 2, "name": "Second", "season": 1, "number": 2, "airdate": "2020-01-08", "url": "u2" }
            ]
            """);

        var episodes = await client.GetEpisodesAsync(99);

        episodes.Should().HaveCount(2);
        episodes[0].Name.Should().Be("Pilot");
        episodes[1].Number.Should().Be(2);
    }
}
