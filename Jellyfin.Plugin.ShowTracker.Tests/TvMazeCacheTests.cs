using FluentAssertions;
using Jellyfin.Plugin.ShowTracker.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ShowTracker.Tests;

public class TvMazeCacheTests : IDisposable
{
    private readonly string _tempDir;
    private int _ttlHours = 12;

    public TvMazeCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "showtracker_cache_tests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private TvMazeCache CreateCache() => new(
        NullLogger<TvMazeCache>.Instance,
        _tempDir,
        () => _ttlHours);

    private sealed record Sample(string Name, int Value);

    [Fact]
    public void Set_ThenTryGet_ReturnsValueFromMemory()
    {
        var cache = CreateCache();
        var payload = new Sample("hello", 42);

        cache.Set("k1", payload);

        cache.TryGet<Sample>("k1", out var retrieved).Should().BeTrue();
        retrieved.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void TryGet_FallsBackToDisk_WhenMemoryEmpty()
    {
        // Seed the disk via one cache instance, then read with a fresh one.
        var first = CreateCache();
        first.Set("k2", new Sample("disk", 7));

        var second = CreateCache(); // separate in-memory dictionary, same dir
        second.TryGet<Sample>("k2", out var retrieved).Should().BeTrue();
        retrieved!.Name.Should().Be("disk");
        retrieved.Value.Should().Be(7);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenEntryExpired()
    {
        var cache = CreateCache();
        cache.Set("k3", new Sample("stale", 1));

        // Re-read with TTL of 0 hours — anything saved more than 0 hours ago is expired.
        // Set to a negative value so freshly-written entries are immediately considered stale.
        _ttlHours = -1;

        cache.TryGet<Sample>("k3", out var retrieved).Should().BeFalse();
        retrieved.Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesMemoryAndDiskEntries()
    {
        var cache = CreateCache();
        cache.Set("k4", new Sample("bye", 9));

        cache.Clear();

        cache.TryGet<Sample>("k4", out _).Should().BeFalse();
        Directory.GetFiles(_tempDir, "*.json").Should().BeEmpty();
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownKey()
    {
        var cache = CreateCache();
        cache.TryGet<Sample>("missing", out var retrieved).Should().BeFalse();
        retrieved.Should().BeNull();
    }
}
