using FluentAssertions;
using Jellyfin.Plugin.ShowTracker.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ShowTracker.Tests;

public class TvMazeRateLimiterTests
{
    [Fact]
    public async Task WaitForSlot_AllowsBurstUpToWindowLimit()
    {
        var limiter = new TvMazeRateLimiter(NullLogger<TvMazeRateLimiter>.Instance);

        // The limiter exposes the cap via a constant (18 requests / 10s).
        // 18 sequential calls should all return promptly without delay.
        var start = DateTime.UtcNow;
        for (var i = 0; i < 18; i++)
        {
            await limiter.WaitForSlotAsync();
        }

        var elapsed = DateTime.UtcNow - start;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "the first 18 calls should fit inside the rate window without blocking");
    }

    [Fact]
    public async Task WaitForSlot_BlocksWhenWindowIsSaturated()
    {
        var limiter = new TvMazeRateLimiter(NullLogger<TvMazeRateLimiter>.Instance);

        // Fill the window.
        for (var i = 0; i < 18; i++)
        {
            await limiter.WaitForSlotAsync();
        }

        // The 19th call must wait until the oldest timestamp falls out of the 10 s window.
        // To keep the test fast we cancel after a short delay and confirm the call was still pending.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var act = async () => await limiter.WaitForSlotAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "the limiter must block additional calls once the window is full");
    }

    [Fact]
    public async Task WaitForSlot_IsSafeUnderConcurrency()
    {
        var limiter = new TvMazeRateLimiter(NullLogger<TvMazeRateLimiter>.Instance);

        // 18 concurrent waits should all complete; nothing should deadlock or double-release.
        var tasks = Enumerable.Range(0, 18)
            .Select(_ => limiter.WaitForSlotAsync())
            .ToArray();

        var act = async () => await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(2));
        await act.Should().NotThrowAsync();
        tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
    }
}
