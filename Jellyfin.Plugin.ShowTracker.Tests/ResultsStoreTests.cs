using FluentAssertions;
using Jellyfin.Plugin.ShowTracker.Models;
using Jellyfin.Plugin.ShowTracker.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ShowTracker.Tests;

public class ResultsStoreTests : IDisposable
{
    private readonly string _tempDir;

    public ResultsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "showtracker_results_tests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private ResultsStore CreateStore() => new(NullLogger<ResultsStore>.Instance, _tempDir);

    [Fact]
    public void Load_ReturnsNull_BeforeAnyScan()
    {
        var store = CreateStore();
        store.Load().Should().BeNull();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsResult()
    {
        var store = CreateStore();
        var result = new ScanResult
        {
            ScanCompletedUtc = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc),
            ScanDuration = TimeSpan.FromSeconds(30),
            LookupFailures = 2,
            Shows = new List<ShowReport>
            {
                new()
                {
                    ShowName = "Breaking Bad",
                    JellyfinId = "jf-1",
                    TvMazeId = 169,
                    Status = "Ended",
                    MissingEpisodes = new List<MissingEpisode>
                    {
                        new() { SeasonNumber = 5, EpisodeNumber = 14, Name = "Ozymandias" }
                    }
                }
            }
        };

        store.Save(result);

        var loaded = store.Load();
        loaded.Should().NotBeNull();
        loaded!.LookupFailures.Should().Be(2);
        loaded.Shows.Should().ContainSingle();
        loaded.Shows[0].ShowName.Should().Be("Breaking Bad");
        loaded.Shows[0].MissingEpisodes.Should().ContainSingle()
            .Which.Name.Should().Be("Ozymandias");
    }

    [Fact]
    public void Load_ReadsFromDisk_OnFreshInstance()
    {
        var first = CreateStore();
        first.Save(new ScanResult { LookupFailures = 7 });

        var second = CreateStore(); // separate in-memory cache, same dir
        var loaded = second.Load();

        loaded.Should().NotBeNull();
        loaded!.LookupFailures.Should().Be(7);
    }

    [Fact]
    public void Save_OverwritesPreviousResult()
    {
        var store = CreateStore();
        store.Save(new ScanResult { LookupFailures = 1 });
        store.Save(new ScanResult { LookupFailures = 99 });

        store.Load()!.LookupFailures.Should().Be(99);
    }
}
