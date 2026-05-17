using Jellyfin.Plugin.ShowTracker.Api.Models;

namespace Jellyfin.Plugin.ShowTracker.Services;

/// <summary>
/// Pure matching logic for diffing local episodes against TVMaze episodes.
/// Handles Jellyfin libraries that use a different season/numbering scheme than TVMaze
/// (split-cour anime, absolute numbering, etc.) by trying several fallback strategies.
/// </summary>
public static class EpisodeMatcher
{
    /// <summary>
    /// Minimal episode shape extracted from a Jellyfin <c>Episode</c>.
    /// </summary>
    public sealed class LocalEpisode
    {
        /// <summary>Gets or sets the season number from <c>ParentIndexNumber</c>.</summary>
        public int? Season { get; set; }

        /// <summary>Gets or sets the in-season episode number from <c>IndexNumber</c>.</summary>
        public int? Number { get; set; }

        /// <summary>Gets or sets the second episode number for double-episodes (<c>IndexNumberEnd</c>).</summary>
        public int? NumberEnd { get; set; }

        /// <summary>Gets or sets the episode title.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the premiere date.</summary>
        public DateTime? PremiereDate { get; set; }
    }

    /// <summary>
    /// Describes how the matcher arrived at its conclusion for a show.
    /// </summary>
    public enum MatchMode
    {
        /// <summary>All local episodes matched a TVMaze episode by season×number.</summary>
        BySeasonEpisode,

        /// <summary>Some episodes matched only after falling back to title/airdate.</summary>
        WithNameOrAirdateFallback,

        /// <summary>Local library uses absolute episode numbering (all episodes in one season).</summary>
        ByAbsoluteNumbering,

        /// <summary>Local library has far more episodes than TVMaze claims — the TVMaze ID is probably wrong.</summary>
        PossiblyWrongShow,

        /// <summary>No reliable signal that local matches this TVMaze entry.</summary>
        NoMatch
    }

    /// <summary>
    /// Outcome of a comparison.
    /// </summary>
    public sealed class Result
    {
        /// <summary>Gets the TVMaze episodes that no local file appears to cover.</summary>
        public List<TvMazeEpisode> Missing { get; init; } = new();

        /// <summary>Gets the strategy that produced the strongest matches.</summary>
        public MatchMode Mode { get; init; }

        /// <summary>Gets a 0..1 confidence score: fraction of TVMaze episodes that found a local match.</summary>
        public double Confidence { get; init; }
    }

    /// <summary>
    /// Compares a local episode list against TVMaze's episode list and returns the unmatched (missing) TVMaze episodes
    /// along with a confidence score and the matching strategy that worked.
    /// </summary>
    /// <param name="tvMaze">TVMaze episodes (already filtered for specials/unaired per config).</param>
    /// <param name="local">Local Jellyfin episodes.</param>
    /// <returns>The match result.</returns>
    public static Result Match(IReadOnlyList<TvMazeEpisode> tvMaze, IReadOnlyList<LocalEpisode> local)
    {
        ArgumentNullException.ThrowIfNull(tvMaze);
        ArgumentNullException.ThrowIfNull(local);

        if (tvMaze.Count == 0)
        {
            return new Result { Mode = MatchMode.BySeasonEpisode, Confidence = 1.0 };
        }

        // If the local library has dramatically more episodes than TVMaze, the lookup probably
        // landed on the wrong entry (e.g. Cowboy Bebop anime matched to Netflix live-action).
        // We still report missing-by-S×E so the user sees the data, but tag the mode.
        var likelyWrongShow = local.Count > 0
            && tvMaze.Count > 0
            && local.Count >= tvMaze.Count * 2
            && local.Count >= 10;

        // Index local episodes for cheap lookup.
        var localBySxE = new HashSet<(int s, int e)>();
        foreach (var l in local)
        {
            if (!l.Season.HasValue || !l.Number.HasValue) continue;
            localBySxE.Add((l.Season.Value, l.Number.Value));
            // Double-episodes (S01E01-02 stored as one file) cover the end number too.
            if (l.NumberEnd.HasValue && l.NumberEnd.Value > l.Number.Value)
            {
                for (var n = l.Number.Value + 1; n <= l.NumberEnd.Value; n++)
                {
                    localBySxE.Add((l.Season.Value, n));
                }
            }
        }

        var localByName = local
            .Where(l => !string.IsNullOrWhiteSpace(l.Name))
            .GroupBy(l => NormalizeTitle(l.Name!))
            .ToDictionary(g => g.Key, g => g.First());

        var localByAirdate = local
            .Where(l => l.PremiereDate.HasValue)
            .GroupBy(l => DateOnly.FromDateTime(l.PremiereDate!.Value))
            .ToDictionary(g => g.Key, g => g.First());

        // Pass 1: strict season × number.
        var stillMissing = new List<TvMazeEpisode>();
        var fellBack = 0;
        foreach (var tvEp in tvMaze)
        {
            if (!tvEp.Number.HasValue) continue;
            if (localBySxE.Contains((tvEp.Season, tvEp.Number.Value)))
            {
                continue;
            }

            // Pass 2 fallback: title or airdate.
            if (!string.IsNullOrWhiteSpace(tvEp.Name)
                && localByName.ContainsKey(NormalizeTitle(tvEp.Name)))
            {
                fellBack++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tvEp.Airdate)
                && DateOnly.TryParse(tvEp.Airdate, out var airDate)
                && localByAirdate.ContainsKey(airDate))
            {
                fellBack++;
                continue;
            }

            stillMissing.Add(tvEp);
        }

        // Pass 3: absolute-numbering fallback.
        // If the strict pass left most TVMaze episodes "missing" but the local count is similar,
        // the library probably uses absolute numbering (common for anime). Re-key TVMaze by absolute
        // order (sorted by season, then number) and see if local IndexNumber lines up 1-to-1.
        var matchedByAbsolute = 0;
        var localInSingleSeason = local.All(l => !l.Season.HasValue || l.Season.Value <= 1);
        var tvMazeSpansMultipleSeasons = tvMaze.Any(e => e.Season > 1);
        if (localInSingleSeason
            && tvMazeSpansMultipleSeasons
            && local.Count >= tvMaze.Count * 0.5
            && stillMissing.Count > 0)
        {
            var orderedTvMaze = tvMaze
                .Where(e => e.Number.HasValue)
                .OrderBy(e => e.Season).ThenBy(e => e.Number!.Value)
                .ToList();

            var localAbsoluteIndices = new HashSet<int>(
                local.Where(l => l.Number.HasValue).Select(l => l.Number!.Value));

            var absoluteMissing = new List<TvMazeEpisode>();
            for (var i = 0; i < orderedTvMaze.Count; i++)
            {
                if (!localAbsoluteIndices.Contains(i + 1))
                {
                    absoluteMissing.Add(orderedTvMaze[i]);
                }
                else
                {
                    matchedByAbsolute++;
                }
            }

            // Only adopt absolute mode if it explains MORE matches than the strict+fallback pass.
            var strictMatched = tvMaze.Count - stillMissing.Count;
            if (matchedByAbsolute > strictMatched)
            {
                var confidence = (double)matchedByAbsolute / tvMaze.Count;
                return new Result
                {
                    Missing = absoluteMissing,
                    Mode = likelyWrongShow ? MatchMode.PossiblyWrongShow : MatchMode.ByAbsoluteNumbering,
                    Confidence = confidence
                };
            }
        }

        var matched = tvMaze.Count - stillMissing.Count;
        var confidenceVal = (double)matched / tvMaze.Count;

        MatchMode mode;
        if (likelyWrongShow)
        {
            mode = MatchMode.PossiblyWrongShow;
        }
        else if (confidenceVal < 0.1)
        {
            mode = MatchMode.NoMatch;
        }
        else if (fellBack > 0)
        {
            mode = MatchMode.WithNameOrAirdateFallback;
        }
        else
        {
            mode = MatchMode.BySeasonEpisode;
        }

        return new Result
        {
            Missing = stillMissing,
            Mode = mode,
            Confidence = confidenceVal
        };
    }

    /// <summary>
    /// Normalizes an episode title for fuzzy comparison: lowercase, ASCII letters/digits only,
    /// strip a leading "the/a/an", and collapse multi-part suffixes like " (1)" / " - Part 2".
    /// </summary>
    /// <param name="title">Title to normalize.</param>
    /// <returns>The normalized form.</returns>
    public static string NormalizeTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;

        var lower = title.ToLowerInvariant();
        var buf = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (c >= 'a' && c <= 'z') buf.Append(c);
            else if (c >= '0' && c <= '9') buf.Append(c);
        }
        var s = buf.ToString();
        if (s.StartsWith("the", StringComparison.Ordinal) && s.Length > 3) s = s[3..];
        else if (s.StartsWith("a", StringComparison.Ordinal) && s.Length > 1 && !char.IsLetter(s[1])) s = s[1..];
        else if (s.StartsWith("an", StringComparison.Ordinal) && s.Length > 2) s = s[2..];
        return s;
    }
}
