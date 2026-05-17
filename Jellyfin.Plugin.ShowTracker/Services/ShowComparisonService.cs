using System.Diagnostics;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ShowTracker.Api;
using Jellyfin.Plugin.ShowTracker.Models;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowTracker.Services;

/// <summary>
/// Core comparison engine. Scans the Jellyfin library and diffs against TVMaze data.
/// </summary>
public class ShowComparisonService
{
    private readonly ILibraryManager _libraryManager;
    private readonly TvMazeApiClient _tvMazeClient;
    private readonly ResultsStore _resultsStore;
    private readonly ILogger<ShowComparisonService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowComparisonService"/> class.
    /// </summary>
    public ShowComparisonService(
        ILibraryManager libraryManager,
        TvMazeApiClient tvMazeClient,
        ResultsStore resultsStore,
        ILogger<ShowComparisonService> logger)
    {
        _libraryManager = libraryManager;
        _tvMazeClient = tvMazeClient;
        _resultsStore = resultsStore;
        _logger = logger;
    }

    /// <summary>
    /// Runs a full scan comparing the local library against TVMaze.
    /// </summary>
    /// <param name="progress">Progress reporter (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completed scan result.</returns>
    public async Task<ScanResult> RunScanAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

        _logger.LogInformation("Starting missing episode scan...");

        // Get all TV series from the library
        var allSeries = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Series },
            IsVirtualItem = false,
            Recursive = true
        }).OfType<Series>().ToList();

        // Filter out excluded libraries
        if (config.ExcludedLibraries.Length > 0)
        {
            allSeries = allSeries
                .Where(s => !config.ExcludedLibraries.Contains(
                    s.GetTopParent()?.Name ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter out dismissed shows
        var dismissedSet = new HashSet<string>(config.DismissedShows, StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("Found {Count} TV series to scan", allSeries.Count);

        var reports = new List<ShowReport>();
        var lookupFailures = 0;

        for (int i = 0; i < allSeries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var series = allSeries[i];
            var percent = (double)(i + 1) / allSeries.Count * 100;
            progress.Report(percent);

            // Skip dismissed shows
            if (dismissedSet.Contains(series.Id.ToString()))
            {
                continue;
            }

            try
            {
                var report = await ProcessSeriesAsync(series, config, cancellationToken).ConfigureAwait(false);
                if (report != null)
                {
                    reports.Add(report);
                }
                else
                {
                    lookupFailures++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing series: {SeriesName}", series.Name);
                lookupFailures++;
            }
        }

        stopwatch.Stop();

        var result = new ScanResult
        {
            ScanCompletedUtc = DateTime.UtcNow,
            ScanDuration = stopwatch.Elapsed,
            Shows = reports.OrderByDescending(r => r.TotalMissingCount).ToList(),
            LookupFailures = lookupFailures
        };

        _resultsStore.Save(result);

        _logger.LogInformation(
            "Scan complete in {Duration}. {Total} shows scanned, {Missing} with missing episodes, {Failures} lookup failures",
            stopwatch.Elapsed,
            result.TotalShows,
            result.IncompleteShows,
            result.LookupFailures);

        return result;
    }

    private async Task<ShowReport?> ProcessSeriesAsync(
        Series series,
        Configuration.PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        // Try to find the show on TVMaze using provider IDs
        var tvMazeShow = await LookupShowAsync(series, cancellationToken).ConfigureAwait(false);
        if (tvMazeShow == null)
        {
            _logger.LogDebug("Could not find TVMaze match for: {SeriesName}", series.Name);
            return null;
        }

        _logger.LogDebug("Matched {SeriesName} to TVMaze ID {TvMazeId}", series.Name, tvMazeShow.Id);

        // Get episodes from TVMaze
        var tvMazeEpisodes = await _tvMazeClient.GetEpisodesAsync(
            tvMazeShow.Id,
            config.IncludeSpecials,
            cancellationToken).ConfigureAwait(false);

        // Filter out unaired episodes unless configured to include them
        if (!config.IncludeUnaired)
        {
            tvMazeEpisodes = tvMazeEpisodes.Where(e => e.HasAired).ToList();
        }

        // Filter out specials if not included
        if (!config.IncludeSpecials)
        {
            tvMazeEpisodes = tvMazeEpisodes.Where(e => !e.IsSpecial).ToList();
        }

        // Get local episodes from Jellyfin
        var localEpisodes = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            AncestorIds = new[] { series.Id },
            IsVirtualItem = false,
            Recursive = true
        }).OfType<Episode>().ToList();

        // Build a set of local episode keys (season, episode number)
        var localEpisodeKeys = new HashSet<string>();
        var localSeasonNumbers = new HashSet<int>();

        foreach (var ep in localEpisodes)
        {
            if (ep.ParentIndexNumber.HasValue)
            {
                localSeasonNumbers.Add(ep.ParentIndexNumber.Value);

                if (ep.IndexNumber.HasValue)
                {
                    localEpisodeKeys.Add($"{ep.ParentIndexNumber.Value}x{ep.IndexNumber.Value}");
                }
            }
        }

        // Find missing episodes
        var missingEpisodes = new List<MissingEpisode>();
        foreach (var tvEp in tvMazeEpisodes)
        {
            if (!tvEp.Number.HasValue)
            {
                continue; // Skip episodes without a number (some specials)
            }

            var key = $"{tvEp.Season}x{tvEp.Number.Value}";
            if (!localEpisodeKeys.Contains(key))
            {
                missingEpisodes.Add(new MissingEpisode
                {
                    SeasonNumber = tvEp.Season,
                    EpisodeNumber = tvEp.Number,
                    Name = tvEp.Name,
                    Airdate = tvEp.Airdate,
                    TvMazeUrl = tvEp.Url,
                    HasAired = tvEp.HasAired
                });
            }
        }

        // Determine missing seasons (seasons on TVMaze with zero local episodes)
        var tvMazeSeasonNumbers = tvMazeEpisodes
            .Select(e => e.Season)
            .Where(s => s > 0) // Exclude specials season (0)
            .Distinct()
            .ToHashSet();

        var tvMazeSeasons = await _tvMazeClient.GetSeasonsAsync(tvMazeShow.Id, cancellationToken).ConfigureAwait(false);

        var missingSeasons = new List<MissingSeason>();
        foreach (var seasonNum in tvMazeSeasonNumbers)
        {
            if (!localSeasonNumbers.Contains(seasonNum))
            {
                var seasonInfo = tvMazeSeasons.FirstOrDefault(s => s.Number == seasonNum);
                var episodesInSeason = tvMazeEpisodes.Count(e => e.Season == seasonNum);

                missingSeasons.Add(new MissingSeason
                {
                    SeasonNumber = seasonNum,
                    ExpectedEpisodeCount = episodesInSeason,
                    PremiereDate = seasonInfo?.PremiereDate,
                    TvMazeUrl = seasonInfo?.Url ?? tvMazeShow.Url
                });
            }
        }

        return new ShowReport
        {
            ShowName = series.Name,
            JellyfinId = series.Id.ToString(),
            TvMazeId = tvMazeShow.Id,
            TvMazeUrl = tvMazeShow.Url,
            Status = tvMazeShow.Status,
            TotalSeasonsOnTvMaze = tvMazeSeasonNumbers.Count,
            TotalSeasonsLocal = localSeasonNumbers.Count(s => s > 0), // Exclude specials
            TotalEpisodesOnTvMaze = tvMazeEpisodes.Count,
            TotalEpisodesLocal = localEpisodes.Count,
            MissingSeasons = missingSeasons,
            MissingEpisodes = missingEpisodes,
            ImageUrl = tvMazeShow.Image?.Medium
        };
    }

    private async Task<Api.Models.TvMazeShow?> LookupShowAsync(
        Series series,
        CancellationToken cancellationToken)
    {
        // Try TheTVDB ID first (most common in Jellyfin)
        if (series.ProviderIds.TryGetValue("Tvdb", out var tvdbId) && !string.IsNullOrEmpty(tvdbId))
        {
            var show = await _tvMazeClient.LookupByTvdbAsync(tvdbId, cancellationToken).ConfigureAwait(false);
            if (show != null)
            {
                return show;
            }
        }

        // Fall back to IMDB ID
        if (series.ProviderIds.TryGetValue("Imdb", out var imdbId) && !string.IsNullOrEmpty(imdbId))
        {
            var show = await _tvMazeClient.LookupByImdbAsync(imdbId, cancellationToken).ConfigureAwait(false);
            if (show != null)
            {
                return show;
            }
        }

        return null;
    }
}
