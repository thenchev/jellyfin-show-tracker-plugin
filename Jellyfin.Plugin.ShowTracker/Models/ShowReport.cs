namespace Jellyfin.Plugin.ShowTracker.Models;

/// <summary>
/// Report for a single show comparing local library to TVMaze data.
/// </summary>
public class ShowReport
{
    /// <summary>
    /// Gets or sets the show name.
    /// </summary>
    public string ShowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin internal ID for this series.
    /// </summary>
    public string JellyfinId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TVMaze show ID.
    /// </summary>
    public int TvMazeId { get; set; }

    /// <summary>
    /// Gets or sets the TVMaze URL for attribution (CC BY-SA 4.0).
    /// </summary>
    public string TvMazeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the show status from TVMaze (Running, Ended, etc.).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the total number of seasons on TVMaze.
    /// </summary>
    public int TotalSeasonsOnTvMaze { get; set; }

    /// <summary>
    /// Gets or sets the total number of seasons in the local library.
    /// </summary>
    public int TotalSeasonsLocal { get; set; }

    /// <summary>
    /// Gets or sets the total episodes on TVMaze (after filtering).
    /// </summary>
    public int TotalEpisodesOnTvMaze { get; set; }

    /// <summary>
    /// Gets or sets the total episodes in the local library.
    /// </summary>
    public int TotalEpisodesLocal { get; set; }

    /// <summary>
    /// Gets or sets the list of entirely missing seasons.
    /// </summary>
    public List<MissingSeason> MissingSeasons { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of missing episodes.
    /// </summary>
    public List<MissingEpisode> MissingEpisodes { get; set; } = new();

    /// <summary>
    /// Gets or sets the poster image URL from TVMaze.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the matching strategy used to compare local episodes to TVMaze.
    /// See <see cref="Services.EpisodeMatcher.MatchMode"/>.
    /// </summary>
    public string MatchMode { get; set; } = nameof(Services.EpisodeMatcher.MatchMode.BySeasonEpisode);

    /// <summary>
    /// Gets or sets a 0..1 score for how confident we are that the local library was correctly matched.
    /// </summary>
    public double MatchConfidence { get; set; } = 1.0;

    /// <summary>
    /// Gets the total number of missing items (seasons counted as their episode count + individual episodes).
    /// </summary>
    public int TotalMissingCount => MissingEpisodes.Count;

    /// <summary>
    /// Gets a value indicating whether this show is complete.
    /// </summary>
    public bool IsComplete => MissingSeasons.Count == 0 && MissingEpisodes.Count == 0;
}
