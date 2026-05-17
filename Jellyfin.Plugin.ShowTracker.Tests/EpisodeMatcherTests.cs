using FluentAssertions;
using Jellyfin.Plugin.ShowTracker.Api.Models;
using Jellyfin.Plugin.ShowTracker.Services;

namespace Jellyfin.Plugin.ShowTracker.Tests;

public class EpisodeMatcherTests
{
    private static TvMazeEpisode TvEp(int season, int number, string name = "ep", string? airdate = "2000-01-01")
        => new() { Season = season, Number = number, Name = name, Airdate = airdate };

    private static EpisodeMatcher.LocalEpisode Local(int season, int number, string? name = null, DateTime? airdate = null)
        => new() { Season = season, Number = number, Name = name, PremiereDate = airdate };

    [Fact]
    public void Match_StrictBySeasonEpisode_AllPresent_NothingMissing()
    {
        var tvMaze = new[] { TvEp(1, 1), TvEp(1, 2), TvEp(2, 1) };
        var local = new[] { Local(1, 1), Local(1, 2), Local(2, 1) };

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Missing.Should().BeEmpty();
        result.Mode.Should().Be(EpisodeMatcher.MatchMode.BySeasonEpisode);
        result.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Match_OneEpisodeGap_ReportsIt()
    {
        var tvMaze = new[] { TvEp(1, 1), TvEp(1, 2), TvEp(1, 3) };
        var local = new[] { Local(1, 1), Local(1, 3) };

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Missing.Should().HaveCount(1);
        result.Missing[0].Number.Should().Be(2);
    }

    [Fact]
    public void Match_DoubleEpisode_CoversBothNumbers()
    {
        // Local file is "S01E01-02" stored as one episode with IndexNumberEnd=2.
        var tvMaze = new[] { TvEp(1, 1), TvEp(1, 2), TvEp(1, 3) };
        var local = new[]
        {
            new EpisodeMatcher.LocalEpisode { Season = 1, Number = 1, NumberEnd = 2 },
            Local(1, 3)
        };

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Missing.Should().BeEmpty();
    }

    [Fact]
    public void Match_NameFallback_WhenSeasonNumberingDiffers()
    {
        // TVMaze splits a single cour into S2; local files are tagged S1E26 etc., but episode titles match.
        var tvMaze = new[]
        {
            TvEp(2, 1, "Premiere"),
            TvEp(2, 2, "Aftermath")
        };
        var local = new[]
        {
            Local(1, 26, "Premiere"),
            Local(1, 27, "Aftermath")
        };

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Missing.Should().BeEmpty();
        result.Mode.Should().Be(EpisodeMatcher.MatchMode.WithNameOrAirdateFallback);
    }

    [Fact]
    public void Match_AirdateFallback_WhenNamesDifferButDatesAlign()
    {
        var tvMaze = new[]
        {
            TvEp(2, 1, "TVMaze Title", "2020-03-01"),
            TvEp(2, 2, "Another", "2020-03-08")
        };
        var local = new[]
        {
            Local(1, 26, name: "Local Title A", airdate: new DateTime(2020, 3, 1)),
            Local(1, 27, name: "Local Title B", airdate: new DateTime(2020, 3, 8))
        };

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Missing.Should().BeEmpty();
    }

    [Fact]
    public void Match_AbsoluteNumbering_AnimeLikeLayout_DetectsItAndCountsCorrectly()
    {
        // TVMaze: S1(2), S2(3), S3(2) = 7 absolute episodes.
        var tvMaze = new[]
        {
            TvEp(1, 1, "a"), TvEp(1, 2, "b"),
            TvEp(2, 1, "c"), TvEp(2, 2, "d"), TvEp(2, 3, "e"),
            TvEp(3, 1, "f"), TvEp(3, 2, "g")
        };
        // Local library has all 7 episodes but tagged as Season 1, eps 1..7 (absolute).
        var local = Enumerable.Range(1, 7)
            .Select(i => Local(1, i))
            .ToList();

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Missing.Should().BeEmpty();
        result.Mode.Should().Be(EpisodeMatcher.MatchMode.ByAbsoluteNumbering);
    }

    [Fact]
    public void Match_AbsoluteNumbering_WithGap_ReportsGapByAbsolutePosition()
    {
        var tvMaze = new[]
        {
            TvEp(1, 1), TvEp(1, 2),
            TvEp(2, 1), TvEp(2, 2)
        };
        // Local: 1, 2, 4 (absolute) — absolute index 3 (= TVMaze S2E1) is missing.
        var local = new[] { Local(1, 1), Local(1, 2), Local(1, 4) };

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Mode.Should().Be(EpisodeMatcher.MatchMode.ByAbsoluteNumbering);
        result.Missing.Should().HaveCount(1);
        result.Missing[0].Season.Should().Be(2);
        result.Missing[0].Number.Should().Be(1);
    }

    [Fact]
    public void Match_PossiblyWrongShow_WhenLocalMassivelyOutnumbersTvMaze()
    {
        // Local has 62 eps but TVMaze only knows about 16 — looks like a wrong-show match.
        var tvMaze = Enumerable.Range(1, 16).Select(i => TvEp(1, i)).ToArray();
        var local = Enumerable.Range(1, 62).Select(i => Local(1, i)).ToList();

        var result = EpisodeMatcher.Match(tvMaze, local);

        result.Mode.Should().Be(EpisodeMatcher.MatchMode.PossiblyWrongShow);
    }

    [Fact]
    public void Match_EmptyTvMaze_ReturnsCompleteByDefinition()
    {
        var result = EpisodeMatcher.Match(Array.Empty<TvMazeEpisode>(), Array.Empty<EpisodeMatcher.LocalEpisode>());

        result.Missing.Should().BeEmpty();
        result.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void NormalizeTitle_StripsPunctuationAndCase()
    {
        EpisodeMatcher.NormalizeTitle("The Fall, of Shiganshina (1)")
            .Should().Be("fallofshiganshina1");
    }

    [Fact]
    public void NormalizeTitle_HandlesEmpty()
    {
        EpisodeMatcher.NormalizeTitle("").Should().Be("");
    }
}
