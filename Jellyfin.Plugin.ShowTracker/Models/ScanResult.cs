namespace Jellyfin.Plugin.ShowTracker.Models;

/// <summary>
/// Complete scan result containing all show reports and metadata.
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Gets or sets the timestamp when the scan completed.
    /// </summary>
    public DateTime ScanCompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the duration of the scan.
    /// </summary>
    public TimeSpan ScanDuration { get; set; }

    /// <summary>
    /// Gets or sets the list of show reports.
    /// </summary>
    public List<ShowReport> Shows { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of shows that could not be looked up on TVMaze.
    /// </summary>
    public int LookupFailures { get; set; }

    /// <summary>
    /// Gets the total number of tracked shows.
    /// </summary>
    public int TotalShows => Shows.Count;

    /// <summary>
    /// Gets the number of shows with no missing episodes.
    /// </summary>
    public int CompleteShows => Shows.Count(s => s.IsComplete);

    /// <summary>
    /// Gets the number of shows with missing episodes or seasons.
    /// </summary>
    public int IncompleteShows => Shows.Count(s => !s.IsComplete);

    /// <summary>
    /// Gets the total number of missing seasons across all shows.
    /// </summary>
    public int TotalMissingSeasons => Shows.Sum(s => s.MissingSeasons.Count);

    /// <summary>
    /// Gets the total number of missing episodes across all shows.
    /// </summary>
    public int TotalMissingEpisodes => Shows.Sum(s => s.MissingEpisodes.Count);
}
