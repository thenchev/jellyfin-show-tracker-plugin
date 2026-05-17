using System.Text.Json;
using Jellyfin.Plugin.ShowTracker.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowTracker.Services;

/// <summary>
/// Persists scan results to disk as JSON.
/// Thread-safe using ReaderWriterLockSlim.
/// </summary>
public class ResultsStore
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ILogger<ResultsStore> _logger;
    private readonly string _resultsPath;
    private ScanResult? _cachedResult;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultsStore"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ResultsStore(ILogger<ResultsStore> logger)
        : this(
            logger,
            dataPath: Plugin.Instance?.DataPath ?? Path.Combine(Path.GetTempPath(), "showtracker"))
    {
    }

    /// <summary>
    /// Test-friendly constructor allowing an explicit data path.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="dataPath">Directory where scan_results.json will be written.</param>
    public ResultsStore(ILogger<ResultsStore> logger, string dataPath)
    {
        _logger = logger;
        Directory.CreateDirectory(dataPath);
        _resultsPath = Path.Combine(dataPath, "scan_results.json");
    }

    /// <summary>
    /// Saves a scan result to disk and memory.
    /// </summary>
    /// <param name="result">The scan result to save.</param>
    public void Save(ScanResult result)
    {
        _lock.EnterWriteLock();
        try
        {
            _cachedResult = result;
            var json = JsonSerializer.Serialize(result, JsonOptions);
            File.WriteAllText(_resultsPath, json);
            _logger.LogInformation(
                "Saved scan results: {ShowCount} shows, {MissingEps} missing episodes",
                result.TotalShows,
                result.TotalMissingEpisodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scan results to disk");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Loads the latest scan result from memory or disk.
    /// </summary>
    /// <returns>The scan result, or null if no scan has been completed.</returns>
    public ScanResult? Load()
    {
        _lock.EnterReadLock();
        try
        {
            if (_cachedResult != null)
            {
                return _cachedResult;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // Try loading from disk
        _lock.EnterWriteLock();
        try
        {
            if (!File.Exists(_resultsPath))
            {
                return null;
            }

            var json = File.ReadAllText(_resultsPath);
            _cachedResult = JsonSerializer.Deserialize<ScanResult>(json, JsonOptions);
            return _cachedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scan results from disk");
            return null;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
