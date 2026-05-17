namespace Jellyfin.Plugin.ShowTracker.Models;

/// <summary>
/// Represents a missing episode detected by the comparison engine.
/// </summary>
public class MissingEpisode
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    public int? EpisodeNumber { get; set; }

    /// <summary>
    /// Gets or sets the episode name from TVMaze.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the air date (ISO 8601).
    /// </summary>
    public string? Airdate { get; set; }

    /// <summary>
    /// Gets or sets the TVMaze URL for this episode.
    /// </summary>
    public string TvMazeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this episode has already aired.
    /// </summary>
    public bool HasAired { get; set; }

    /// <summary>
    /// Gets a formatted episode code like "S01E05".
    /// </summary>
    public string EpisodeCode =>
        EpisodeNumber.HasValue
            ? $"S{SeasonNumber:D2}E{EpisodeNumber:D2}"
            : $"S{SeasonNumber:D2} Special";
}
