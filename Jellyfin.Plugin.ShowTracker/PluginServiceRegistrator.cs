using Jellyfin.Plugin.ShowTracker.Api;
using Jellyfin.Plugin.ShowTracker.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ShowTracker;

/// <summary>
/// Registers plugin services into Jellyfin's DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<TvMazeRateLimiter>();
        serviceCollection.AddSingleton<TvMazeCache>();
        serviceCollection.AddSingleton<TvMazeApiClient>();
        serviceCollection.AddSingleton<ResultsStore>();
        serviceCollection.AddTransient<ShowComparisonService>();
    }
}
