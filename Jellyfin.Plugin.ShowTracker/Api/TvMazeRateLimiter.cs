using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowTracker.Api;

/// <summary>
/// Rate limiter for TVMaze API calls.
/// TVMaze allows at least 20 calls every 10 seconds per IP address.
/// We use a conservative limit of 18 calls per 10 seconds to stay safely under.
/// </summary>
public class TvMazeRateLimiter
{
    private const int MaxRequests = 18;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<TvMazeRateLimiter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvMazeRateLimiter"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TvMazeRateLimiter(ILogger<TvMazeRateLimiter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Waits until it is safe to make an API call without exceeding the rate limit.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the call can proceed.</returns>
    public async Task WaitForSlotAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var now = DateTime.UtcNow;
                var cutoff = now - Window;

                // Remove timestamps older than the window
                while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoff)
                {
                    _requestTimestamps.Dequeue();
                }

                if (_requestTimestamps.Count < MaxRequests)
                {
                    _requestTimestamps.Enqueue(now);
                    return;
                }

                // Calculate how long to wait until the oldest request falls out of the window
                var oldestInWindow = _requestTimestamps.Peek();
                var waitTime = (oldestInWindow + Window) - now + TimeSpan.FromMilliseconds(100);

                if (waitTime > TimeSpan.Zero)
                {
                    _logger.LogDebug("Rate limit reached, waiting {WaitMs}ms", waitTime.TotalMilliseconds);
                    _semaphore.Release();
                    await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
