using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowTracker.Api.Models;

/// <summary>
/// Represents an episode from the TVMaze API.
/// </summary>
public class TvMazeEpisode
{
    /// <summary>
    /// Gets or sets the TVMaze episode ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the episode name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("season")]
    public int Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number within the season.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the episode type (e.g., "regular", "significant_special", "insignificant_special").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the air date in ISO 8601 format.
    /// </summary>
    [JsonPropertyName("airdate")]
    public string? Airdate { get; set; }

    /// <summary>
    /// Gets or sets the air time.
    /// </summary>
    [JsonPropertyName("airtime")]
    public string? Airtime { get; set; }

    /// <summary>
    /// Gets or sets the runtime in minutes.
    /// </summary>
    [JsonPropertyName("runtime")]
    public int? Runtime { get; set; }

    /// <summary>
    /// Gets or sets the episode URL on TVMaze.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this is a special episode.
    /// </summary>
    [JsonIgnore]
    public bool IsSpecial => Type != null && Type.Contains("special", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether this episode has aired.
    /// </summary>
    [JsonIgnore]
    public bool HasAired
    {
        get
        {
            if (string.IsNullOrEmpty(Airdate))
            {
                return false;
            }

            return DateOnly.TryParse(Airdate, out var airDate) && airDate <= DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }
}
