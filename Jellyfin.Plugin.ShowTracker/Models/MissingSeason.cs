namespace Jellyfin.Plugin.ShowTracker.Models;

/// <summary>
/// Represents an entirely missing season detected by the comparison engine.
/// </summary>
public class MissingSeason
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the expected episode count for this season (from TVMaze).
    /// </summary>
    public int ExpectedEpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets the season premiere date.
    /// </summary>
    public string? PremiereDate { get; set; }

    /// <summary>
    /// Gets or sets the TVMaze URL for this season.
    /// </summary>
    public string TvMazeUrl { get; set; } = string.Empty;
}
