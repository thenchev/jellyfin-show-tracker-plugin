using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.Plugin.ShowTracker.Api.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowTracker.Api;

/// <summary>
/// HTTP client for the TVMaze API with built-in rate limiting, caching, and retry logic.
/// Complies with TVMaze API guidelines: https://www.tvmaze.com/api
/// </summary>
public class TvMazeApiClient : IDisposable
{
    private const string BaseUrl = "https://api.tvmaze.com";
    private const string UserAgent = "JellyfinShowTracker/1.0 (https://github.com/thenchev/jellyfin-show-tracker-plugin)";
    private const int MaxRetries = 3;

    private readonly HttpClient _httpClient;
    private readonly TvMazeRateLimiter _rateLimiter;
    private readonly TvMazeCache _cache;
    private readonly ILogger<TvMazeApiClient> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvMazeApiClient"/> class.
    /// </summary>
    /// <param name="rateLimiter">Rate limiter instance.</param>
    /// <param name="cache">Cache instance.</param>
    /// <param name="logger">Logger instance.</param>
    public TvMazeApiClient(
        TvMazeRateLimiter rateLimiter,
        TvMazeCache cache,
        ILogger<TvMazeApiClient> logger)
    {
        _rateLimiter = rateLimiter;
        _cache = cache;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Looks up a show by its TheTVDB ID.
    /// </summary>
    /// <param name="tvdbId">The TheTVDB ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The TVMaze show, or null if not found.</returns>
    public async Task<TvMazeShow?> LookupByTvdbAsync(string tvdbId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"lookup_tvdb_{tvdbId}";
        if (_cache.TryGet<TvMazeShow>(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache hit for TVDB lookup: {TvdbId}", tvdbId);
            return cached;
        }

        var show = await GetAsync<TvMazeShow>($"/lookup/shows?thetvdb={tvdbId}", cancellationToken).ConfigureAwait(false);
        if (show != null)
        {
            _cache.Set(cacheKey, show);
        }

        return show;
    }

    /// <summary>
    /// Looks up a show by its IMDB ID.
    /// </summary>
    /// <param name="imdbId">The IMDB ID (e.g., "tt0944947").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The TVMaze show, or null if not found.</returns>
    public async Task<TvMazeShow?> LookupByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"lookup_imdb_{imdbId}";
        if (_cache.TryGet<TvMazeShow>(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache hit for IMDB lookup: {ImdbId}", imdbId);
            return cached;
        }

        var show = await GetAsync<TvMazeShow>($"/lookup/shows?imdb={imdbId}", cancellationToken).ConfigureAwait(false);
        if (show != null)
        {
            _cache.Set(cacheKey, show);
        }

        return show;
    }

    /// <summary>
    /// Gets the full episode list for a TVMaze show.
    /// </summary>
    /// <param name="tvMazeShowId">The TVMaze show ID.</param>
    /// <param name="includeSpecials">Whether to include specials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of episodes.</returns>
    public async Task<List<TvMazeEpisode>> GetEpisodesAsync(
        int tvMazeShowId,
        bool includeSpecials = false,
        CancellationToken cancellationToken = default)
    {
        var specialsParam = includeSpecials ? "?specials=1" : string.Empty;
        var cacheKey = $"episodes_{tvMazeShowId}_specials_{includeSpecials}";

        if (_cache.TryGet<List<TvMazeEpisode>>(cacheKey, out var cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for episodes: show {ShowId}", tvMazeShowId);
            return cached;
        }

        var episodes = await GetAsync<List<TvMazeEpisode>>(
            $"/shows/{tvMazeShowId}/episodes{specialsParam}",
            cancellationToken).ConfigureAwait(false);

        var result = episodes ?? new List<TvMazeEpisode>();
        _cache.Set(cacheKey, result);
        return result;
    }

    /// <summary>
    /// Gets the season list for a TVMaze show.
    /// </summary>
    /// <param name="tvMazeShowId">The TVMaze show ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of seasons.</returns>
    public async Task<List<TvMazeSeason>> GetSeasonsAsync(
        int tvMazeShowId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"seasons_{tvMazeShowId}";

        if (_cache.TryGet<List<TvMazeSeason>>(cacheKey, out var cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for seasons: show {ShowId}", tvMazeShowId);
            return cached;
        }

        var seasons = await GetAsync<List<TvMazeSeason>>(
            $"/shows/{tvMazeShowId}/seasons",
            cancellationToken).ConfigureAwait(false);

        var result = seasons ?? new List<TvMazeSeason>();
        _cache.Set(cacheKey, result);
        return result;
    }

    /// <summary>
    /// Generic GET request with rate limiting, retry, and error handling.
    /// </summary>
    private async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _rateLimiter.WaitForSlotAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                _logger.LogDebug("TVMaze API request: {Endpoint} (attempt {Attempt})", endpoint, attempt + 1);

                using var response = await _httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("TVMaze API 404 for: {Endpoint}", endpoint);
                    return default;
                }

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    // Rate limited — back off exponentially
                    var backoffMs = (int)Math.Pow(2, attempt) * 1000;
                    _logger.LogWarning(
                        "TVMaze API rate limited (429). Backing off {BackoffMs}ms (attempt {Attempt}/{MaxRetries})",
                        backoffMs, attempt + 1, MaxRetries);
                    await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<T>(content);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                var backoffMs = (int)Math.Pow(2, attempt) * 1000;
                _logger.LogWarning(ex,
                    "TVMaze API request failed for {Endpoint}. Retrying in {BackoffMs}ms (attempt {Attempt}/{MaxRetries})",
                    endpoint, backoffMs, attempt + 1, MaxRetries);
                await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "TVMaze API request permanently failed for {Endpoint}", endpoint);
                return default;
            }
        }

        return default;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
