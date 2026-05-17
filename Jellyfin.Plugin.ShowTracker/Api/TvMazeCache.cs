using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowTracker.Api;

/// <summary>
/// Two-tier cache (memory + disk) for TVMaze API responses.
/// Reduces API calls and helps stay well within rate limits.
/// </summary>
public class TvMazeCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache = new();
    private readonly ILogger<TvMazeCache> _logger;
    private readonly string _cacheDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvMazeCache"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TvMazeCache(ILogger<TvMazeCache> logger)
    {
        _logger = logger;

        var pluginInstance = Plugin.Instance;
        _cacheDir = pluginInstance != null
            ? Path.Combine(pluginInstance.DataPath, "cache", "tvmaze")
            : Path.Combine(Path.GetTempPath(), "showtracker_cache");

        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Tries to get a cached value.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">The cached value if found.</param>
    /// <returns>True if a valid cached value was found.</returns>
    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        var ttlHours = Plugin.Instance?.Configuration.CacheDurationHours ?? 12;

        // Check memory cache first
        if (_memoryCache.TryGetValue(key, out var entry) && !entry.IsExpired(ttlHours))
        {
            try
            {
                value = JsonSerializer.Deserialize<T>(entry.JsonData);
                return value != null;
            }
            catch (JsonException)
            {
                _memoryCache.TryRemove(key, out _);
            }
        }

        // Fall back to disk cache
        var filePath = GetFilePath(key);
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var diskEntry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(filePath));
            if (diskEntry == null || diskEntry.IsExpired(ttlHours))
            {
                TryDeleteFile(filePath);
                return false;
            }

            // Promote to memory cache
            _memoryCache[key] = diskEntry;
            value = JsonSerializer.Deserialize<T>(diskEntry.JsonData);
            return value != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cache file for key {Key}", key);
            TryDeleteFile(filePath);
            return false;
        }
    }

    /// <summary>
    /// Stores a value in both memory and disk cache.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">The value to cache.</param>
    public void Set<T>(string key, T value)
    {
        var jsonData = JsonSerializer.Serialize(value);
        var entry = new CacheEntry
        {
            JsonData = jsonData,
            CachedAtUtc = DateTime.UtcNow
        };

        _memoryCache[key] = entry;

        // Write to disk asynchronously (best-effort)
        try
        {
            var filePath = GetFilePath(key);
            File.WriteAllText(filePath, JsonSerializer.Serialize(entry));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write cache file for key {Key}", key);
        }
    }

    /// <summary>
    /// Clears all cached data (memory and disk).
    /// </summary>
    public void Clear()
    {
        _memoryCache.Clear();

        try
        {
            if (Directory.Exists(_cacheDir))
            {
                foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
                {
                    TryDeleteFile(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear cache directory");
        }
    }

    private string GetFilePath(string key)
    {
        // Sanitize key for filesystem
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cacheDir, $"{safeKey}.json");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private sealed class CacheEntry
    {
        public string JsonData { get; set; } = string.Empty;

        public DateTime CachedAtUtc { get; set; }

        public bool IsExpired(int ttlHours) =>
            DateTime.UtcNow - CachedAtUtc > TimeSpan.FromHours(ttlHours);
    }
}
