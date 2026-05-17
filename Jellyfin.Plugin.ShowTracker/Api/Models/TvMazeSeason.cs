using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowTracker.Api.Models;

/// <summary>
/// Represents a season from the TVMaze API.
/// </summary>
public class TvMazeSeason
{
    /// <summary>
    /// Gets or sets the TVMaze season ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the episode count for this season.
    /// </summary>
    [JsonPropertyName("episodeOrder")]
    public int? EpisodeOrder { get; set; }

    /// <summary>
    /// Gets or sets the season premiere date.
    /// </summary>
    [JsonPropertyName("premiereDate")]
    public string? PremiereDate { get; set; }

    /// <summary>
    /// Gets or sets the season end date.
    /// </summary>
    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the season URL on TVMaze.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
