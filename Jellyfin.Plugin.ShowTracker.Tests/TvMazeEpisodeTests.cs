using FluentAssertions;
using Jellyfin.Plugin.ShowTracker.Api.Models;

namespace Jellyfin.Plugin.ShowTracker.Tests;

public class TvMazeEpisodeTests
{
    [Fact]
    public void HasAired_True_WhenAirdateInThePast()
    {
        var ep = new TvMazeEpisode { Airdate = "2000-01-01" };
        ep.HasAired.Should().BeTrue();
    }

    [Fact]
    public void HasAired_True_WhenAirdateIsToday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var ep = new TvMazeEpisode { Airdate = today };
        ep.HasAired.Should().BeTrue();
    }

    [Fact]
    public void HasAired_False_WhenAirdateInFuture()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5)).ToString("yyyy-MM-dd");
        var ep = new TvMazeEpisode { Airdate = future };
        ep.HasAired.Should().BeFalse();
    }

    [Fact]
    public void HasAired_False_WhenAirdateMissing()
    {
        new TvMazeEpisode { Airdate = null }.HasAired.Should().BeFalse();
        new TvMazeEpisode { Airdate = string.Empty }.HasAired.Should().BeFalse();
    }

    [Fact]
    public void HasAired_False_WhenAirdateUnparseable()
    {
        new TvMazeEpisode { Airdate = "not-a-date" }.HasAired.Should().BeFalse();
    }

    [Theory]
    [InlineData("significant_special", true)]
    [InlineData("insignificant_special", true)]
    [InlineData("SPECIAL", true)]
    [InlineData("regular", false)]
    [InlineData(null, false)]
    public void IsSpecial_DetectsSpecialTypes(string? type, bool expected)
    {
        new TvMazeEpisode { Type = type }.IsSpecial.Should().Be(expected);
    }
}
