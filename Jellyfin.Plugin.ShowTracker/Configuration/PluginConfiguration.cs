using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ShowTracker.Configuration;

/// <summary>
/// Plugin configuration for Show Tracker.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the cache duration in hours for TVMaze API responses.
    /// Default is 12 hours, which is conservative relative to TVMaze's own 1-hour edge cache.
    /// </summary>
    public int CacheDurationHours { get; set; } = 12;

    /// <summary>
    /// Gets or sets a value indicating whether to include special episodes in the comparison.
    /// Specials are often extras, behind-the-scenes, etc. and may not be commonly collected.
    /// </summary>
    public bool IncludeSpecials { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to include episodes that haven't aired yet.
    /// When true, upcoming episodes will appear in the "missing" list.
    /// </summary>
    public bool IncludeUnaired { get; set; } = false;

    /// <summary>
    /// Gets or sets library names to exclude from scanning.
    /// </summary>
    public string[] ExcludedLibraries { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets Jellyfin series IDs to exclude from scanning (dismissed shows).
    /// </summary>
    public string[] DismissedShows { get; set; } = Array.Empty<string>();
}
