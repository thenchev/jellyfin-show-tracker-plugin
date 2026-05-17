using Jellyfin.Plugin.ShowTracker.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.ShowTracker.ScheduledTasks;

/// <summary>
/// Scheduled task that scans the library for missing episodes/seasons using TVMaze.
/// Appears in Dashboard → Scheduled Tasks as "Check Missing Episodes (TVMaze)".
/// </summary>
public class CheckMissingEpisodesTask : IScheduledTask
{
    private readonly ShowComparisonService _comparisonService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckMissingEpisodesTask"/> class.
    /// </summary>
    /// <param name="comparisonService">The show comparison service.</param>
    public CheckMissingEpisodesTask(ShowComparisonService comparisonService)
    {
        _comparisonService = comparisonService;
    }

    /// <inheritdoc />
    public string Name => "Check Missing Episodes (TVMaze)";

    /// <inheritdoc />
    public string Key => "ShowTrackerCheckMissingEpisodes";

    /// <inheritdoc />
    public string Description => "Scans your TV library and cross-references with TVMaze to find missing episodes and seasons.";

    /// <inheritdoc />
    public string Category => "Show Tracker";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _comparisonService.RunScanAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            // Run daily at 3:00 AM
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        };
    }
}
