using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowTracker.Api.Models;

/// <summary>
/// Represents a TV show from the TVMaze API.
/// </summary>
public class TvMazeShow
{
    /// <summary>
    /// Gets or sets the TVMaze show ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the show name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the show URL on TVMaze.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the show status (e.g., "Running", "Ended").
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the show's premiere date.
    /// </summary>
    [JsonPropertyName("premiered")]
    public string? Premiered { get; set; }

    /// <summary>
    /// Gets or sets the show's end date.
    /// </summary>
    [JsonPropertyName("ended")]
    public string? Ended { get; set; }

    /// <summary>
    /// Gets or sets the show's image URLs.
    /// </summary>
    [JsonPropertyName("image")]
    public TvMazeImage? Image { get; set; }

    /// <summary>
    /// Gets or sets external IDs.
    /// </summary>
    [JsonPropertyName("externals")]
    public TvMazeExternals? Externals { get; set; }
}

/// <summary>
/// Image URLs from TVMaze.
/// </summary>
public class TvMazeImage
{
    /// <summary>
    /// Gets or sets the medium resolution image URL.
    /// </summary>
    [JsonPropertyName("medium")]
    public string? Medium { get; set; }

    /// <summary>
    /// Gets or sets the original resolution image URL.
    /// </summary>
    [JsonPropertyName("original")]
    public string? Original { get; set; }
}

/// <summary>
/// External IDs from TVMaze (thetvdb, imdb, etc.).
/// </summary>
public class TvMazeExternals
{
    /// <summary>
    /// Gets or sets the TheTVDB ID.
    /// </summary>
    [JsonPropertyName("thetvdb")]
    public int? TheTvdb { get; set; }

    /// <summary>
    /// Gets or sets the IMDB ID.
    /// </summary>
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    /// <summary>
    /// Gets or sets the TVRage ID.
    /// </summary>
    [JsonPropertyName("tvrage")]
    public int? TvRage { get; set; }
}
