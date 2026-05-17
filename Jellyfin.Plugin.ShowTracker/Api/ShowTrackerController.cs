using System.Net.Mime;
using Jellyfin.Plugin.ShowTracker.Models;
using Jellyfin.Plugin.ShowTracker.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ShowTracker.Api;

/// <summary>
/// REST API controller for the Show Tracker plugin.
/// Provides endpoints to retrieve scan results and trigger scans.
/// </summary>
[ApiController]
[Route("ShowTracker")]
[Produces(MediaTypeNames.Application.Json)]
public class ShowTrackerController : ControllerBase
{
    private readonly ResultsStore _resultsStore;
    private readonly ShowComparisonService _comparisonService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowTrackerController"/> class.
    /// </summary>
    /// <param name="resultsStore">The results store.</param>
    /// <param name="comparisonService">The comparison service.</param>
    public ShowTrackerController(
        ResultsStore resultsStore,
        ShowComparisonService comparisonService)
    {
        _resultsStore = resultsStore;
        _comparisonService = comparisonService;
    }

    /// <summary>
    /// Gets the full scan results.
    /// </summary>
    /// <returns>The latest scan result or 404 if no scan has been run.</returns>
    [HttpGet("Results")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ScanResult> GetResults()
    {
        var result = _resultsStore.Load();
        if (result == null)
        {
            return NotFound(new { message = "No scan results available. Please run the 'Check Missing Episodes' task first." });
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets scan results for a specific show.
    /// </summary>
    /// <param name="jellyfinSeriesId">The Jellyfin series ID.</param>
    /// <returns>The show report or 404.</returns>
    [HttpGet("Results/{jellyfinSeriesId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ShowReport> GetShowResult(string jellyfinSeriesId)
    {
        var result = _resultsStore.Load();
        var show = result?.Shows.FirstOrDefault(
            s => s.JellyfinId.Equals(jellyfinSeriesId, StringComparison.OrdinalIgnoreCase));

        if (show == null)
        {
            return NotFound(new { message = "Show not found in scan results." });
        }

        return Ok(show);
    }

    /// <summary>
    /// Gets a summary of the scan results (counts only, no details).
    /// </summary>
    /// <returns>Summary statistics.</returns>
    [HttpGet("Summary")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetSummary()
    {
        var result = _resultsStore.Load();
        if (result == null)
        {
            return NotFound(new { message = "No scan results available." });
        }

        return Ok(new
        {
            result.ScanCompletedUtc,
            scanDuration = result.ScanDuration.ToString(),
            result.TotalShows,
            result.CompleteShows,
            result.IncompleteShows,
            result.TotalMissingSeasons,
            result.TotalMissingEpisodes,
            result.LookupFailures
        });
    }

    /// <summary>
    /// Triggers an immediate scan. Requires admin privileges.
    /// </summary>
    /// <returns>Accepted response.</returns>
    [HttpPost("Scan")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public ActionResult TriggerScan([FromServices] ITaskManager taskManager)
    {
        var task = taskManager.ScheduledTasks
            .FirstOrDefault(t => t.ScheduledTask is ScheduledTasks.CheckMissingEpisodesTask);

        if (task == null)
        {
            return NotFound(new { message = "Scheduled task not found." });
        }

        taskManager.Execute(task, new TaskOptions());

        return Accepted(new { message = "Scan started. Check back shortly for results." });
    }

    /// <summary>
    /// Dismisses a show from future reports.
    /// </summary>
    /// <param name="jellyfinSeriesId">The Jellyfin series ID to dismiss.</param>
    /// <returns>Ok response.</returns>
    [HttpPost("Dismiss/{jellyfinSeriesId}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult DismissShow(string jellyfinSeriesId)
    {
        var plugin = Plugin.Instance;
        if (plugin == null)
        {
            return StatusCode(500, new { message = "Plugin not initialized." });
        }

        var config = plugin.Configuration;
        var dismissed = new HashSet<string>(config.DismissedShows, StringComparer.OrdinalIgnoreCase);
        dismissed.Add(jellyfinSeriesId);
        config.DismissedShows = dismissed.ToArray();
        plugin.SaveConfiguration();

        return Ok(new { message = "Show dismissed from future reports." });
    }

    /// <summary>
    /// Un-dismisses a show so it appears in future reports again.
    /// </summary>
    /// <param name="jellyfinSeriesId">The Jellyfin series ID to un-dismiss.</param>
    /// <returns>Ok response.</returns>
    [HttpDelete("Dismiss/{jellyfinSeriesId}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult UndismissShow(string jellyfinSeriesId)
    {
        var plugin = Plugin.Instance;
        if (plugin == null)
        {
            return StatusCode(500, new { message = "Plugin not initialized." });
        }

        var config = plugin.Configuration;
        config.DismissedShows = config.DismissedShows
            .Where(id => !id.Equals(jellyfinSeriesId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        plugin.SaveConfiguration();

        return Ok(new { message = "Show restored to future reports." });
    }
}
