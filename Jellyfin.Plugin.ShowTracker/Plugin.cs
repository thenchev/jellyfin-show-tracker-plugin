using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ShowTracker.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ShowTracker;

/// <summary>
/// Main plugin class for the Show Tracker plugin.
/// Checks your TV library against TVMaze to find missing episodes and seasons.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Show Tracker";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("3754ea39-bfb5-43a2-a17b-25f7bc7fb15e");

    /// <inheritdoc />
    public override string Description => "Tracks missing TV episodes and seasons using TVMaze.";

    /// <summary>
    /// Gets the plugin data path for storing cache and results.
    /// </summary>
    public string DataPath => Path.Combine(ApplicationPaths.PluginConfigurationsPath, "ShowTracker");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "showtracker",
                EmbeddedResourcePath = $"{GetType().Namespace}.Pages.showtracker.html",
                DisplayName = "Show Tracker",
                MenuSection = "server",
                MenuIcon = "tv"
            },
            new PluginPageInfo
            {
                Name = "showtrackerjs",
                EmbeddedResourcePath = $"{GetType().Namespace}.Pages.showtracker.js"
            }
        };
    }
}
