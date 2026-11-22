using FluentAssertions;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Domain.Entities;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.Common;

public class SeasonDateHelperTests
{
    #region TryParseMonthDay

    [Theory]
    [InlineData("01/01", 1, 1)]
    [InlineData("12/31", 12, 31)]
    [InlineData("6/5", 6, 5)]
    [InlineData("11/01", 11, 1)]
    [InlineData("02/28", 2, 28)]
    public void TryParseMonthDay_ValidDateString_ReturnsTrueAndParsesMonthAndDay(
        string input,
        int expectedMonth,
        int expectedDay)
    {
        // Act
        var result = SeasonDateHelper.TryParseMonthDay(input, out var month, out var day);

        // Assert
        result.Should().BeTrue();
        month.Should().Be(expectedMonth);
        day.Should().Be(expectedDay);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseMonthDay_NullOrWhitespace_ReturnsFalseWithZeroOutputs(string? input)
    {
        // Act
        var result = SeasonDateHelper.TryParseMonthDay(input, out var month, out var day);

        // Assert
        result.Should().BeFalse();
        month.Should().Be(0);
        day.Should().Be(0);
    }

    [Theory]
    [InlineData("2025/01/01")]
    [InlineData("01-15")]
    [InlineData("abc/def")]
    [InlineData("01/ab")]
    [InlineData("15")]
    [InlineData("/")]
    public void TryParseMonthDay_InvalidFormat_ReturnsFalse(string input)
    {
        // Act
        var result = SeasonDateHelper.TryParseMonthDay(input, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("00/15")]
    [InlineData("13/01")]
    [InlineData("01/00")]
    [InlineData("01/32")]
    [InlineData("-1/10")]
    [InlineData("05/-5")]
    public void TryParseMonthDay_OutOfRangeValues_ReturnsFalse(string input)
    {
        // Act
        var result = SeasonDateHelper.TryParseMonthDay(input, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsDateInSeasonRange

    [Theory]
    [InlineData(3, 1, true)]   // Start boundary
    [InlineData(4, 15, true)]  // Inside range
    [InlineData(6, 30, true)]  // End boundary
    [InlineData(2, 28, false)] // Day before start
    [InlineData(7, 1, false)]  // Day after end
    public void IsDateInSeasonRange_SameYearSeason_ReturnsExpectedResult(
        int month,
        int day,
        bool expected)
    {
        // Act
        var result = SeasonDateHelper.IsDateInSeasonRange(month, day, "03/01", "06/30");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(11, 1, true)]   // Start boundary
    [InlineData(12, 15, true)]  // Inside range (first year)
    [InlineData(12, 31, true)]  // End of calendar year
    [InlineData(1, 1, true)]    // Start of next calendar year
    [InlineData(1, 15, true)]   // Inside range (next year)
    [InlineData(2, 28, true)]   // End boundary
    [InlineData(10, 31, false)] // Day before start
    [InlineData(3, 1, false)]   // Day after end
    [InlineData(6, 15, false)]  // Mid-year outside range
    public void IsDateInSeasonRange_CrossYearSeason_ReturnsExpectedResult(
        int month,
        int day,
        bool expected)
    {
        // Act
        var result = SeasonDateHelper.IsDateInSeasonRange(month, day, "11/01", "02/28");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid", "06/30")]
    [InlineData("03/01", "invalid")]
    [InlineData("", "")]
    [InlineData("13/01", "02/28")]
    public void IsDateInSeasonRange_InvalidDateStrings_ReturnsFalse(string startDate, string endDate)
    {
        // Act
        var result = SeasonDateHelper.IsDateInSeasonRange(4, 15, startDate, endDate);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region TryGetSeasonDateRange

    [Fact]
    public void TryGetSeasonDateRange_SameYearSeason_ReturnsTrueWithSameYearDates()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Summer-Autumn",
            StartDate = "05/01",
            EndDate = "08/31"
        };

        // Act
        var result = SeasonDateHelper.TryGetSeasonDateRange(season, 2025, out var startDate, out var endDate);

        // Assert
        result.Should().BeTrue();
        startDate.Should().Be(new DateTime(2025, 5, 1));
        endDate.Should().Be(new DateTime(2025, 8, 31));
    }

    [Fact]
    public void TryGetSeasonDateRange_CrossYearSeason_ReturnsTrueWithEndYearIncremented()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Winter-Spring",
            StartDate = "11/01",
            EndDate = "02/28"
        };

        // Act
        var result = SeasonDateHelper.TryGetSeasonDateRange(season, 2025, out var startDate, out var endDate);

        // Assert
        result.Should().BeTrue();
        startDate.Should().Be(new DateTime(2025, 11, 1));
        endDate.Should().Be(new DateTime(2026, 2, 28));
    }

    [Theory]
    [InlineData("invalid", "08/31")]
    [InlineData("05/01", "bad")]
    [InlineData("", "08/31")]
    [InlineData("02/30", "05/01")] // Valid MM/DD bounds, but invalid calendar date
    public void TryGetSeasonDateRange_InvalidSeasonDates_ReturnsFalse(string startStr, string endStr)
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Invalid Season",
            StartDate = startStr,
            EndDate = endStr
        };

        // Act
        var result = SeasonDateHelper.TryGetSeasonDateRange(season, 2025, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ParseSeasonDateToUtc

    [Fact]
    public void ParseSeasonDateToUtc_ValidDateString_ReturnsUtcMidnightDateTime()
    {
        // Act
        var result = SeasonDateHelper.ParseSeasonDateToUtc("03/15", 2025);

        // Assert
        result.Should().Be(new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("13/01")]
    [InlineData("01/32")]
    [InlineData("2025/01/01")]
    public void ParseSeasonDateToUtc_InvalidDateString_ThrowsFormatException(string invalidDate)
    {
        // Act
        var act = () => SeasonDateHelper.ParseSeasonDateToUtc(invalidDate, 2025);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid season date format*");
    }

    #endregion

    #region ResolveCurrentSeason

    [Fact]
    public void ResolveCurrentSeason_ActiveSameYearSeasonMatches_ReturnsSeasonAndReferenceYear()
    {
        // Arrange
        var summerAutumn = new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Summer-Autumn",
            StartDate = "04/01",
            EndDate = "08/31",
            IsActive = true
        };
        var winterSpring = new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Winter-Spring",
            StartDate = "11/01",
            EndDate = "02/28",
            IsActive = true
        };
        var referenceDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = SeasonDateHelper.ResolveCurrentSeason(new[] { winterSpring, summerAutumn }, referenceDate);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Season.Should().BeSameAs(summerAutumn);
        result.Value.Year.Should().Be(2025);
    }

    [Fact]
    public void ResolveCurrentSeason_CrossYearSeasonInDecember_ReturnsCurrentYear()
    {
        // Arrange
        var winterSpring = new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Winter-Spring",
            StartDate = "11/01",
            EndDate = "02/28",
            IsActive = true
        };
        var referenceDate = new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = SeasonDateHelper.ResolveCurrentSeason(new[] { winterSpring }, referenceDate);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Season.Should().BeSameAs(winterSpring);
        result.Value.Year.Should().Be(2025);
    }

    [Fact]
    public void ResolveCurrentSeason_CrossYearSeasonInJanuary_ReturnsPreviousYear()
    {
        // Arrange
        var winterSpring = new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Winter-Spring",
            StartDate = "11/01",
            EndDate = "02/28",
            IsActive = true
        };
        var referenceDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = SeasonDateHelper.ResolveCurrentSeason(new[] { winterSpring }, referenceDate);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Season.Should().BeSameAs(winterSpring);
        result.Value.Year.Should().Be(2025);
    }

    [Fact]
    public void ResolveCurrentSeason_InactiveSeasonsAreSkipped()
    {
        // Arrange
        var inactiveSeason = new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Inactive Season",
            StartDate = "04/01",
            EndDate = "08/31",
            IsActive = false
        };
        var activeSeason = new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Active Season",
            StartDate = "05/01",
            EndDate = "09/30",
            IsActive = true
        };
        var referenceDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var resultWhenOnlyInactive = SeasonDateHelper.ResolveCurrentSeason(new[] { inactiveSeason }, referenceDate);
        var resultWithActiveFallback = SeasonDateHelper.ResolveCurrentSeason(
            new[] { inactiveSeason, activeSeason },
            referenceDate);

        // Assert
        resultWhenOnlyInactive.Should().BeNull();
        resultWithActiveFallback.Should().NotBeNull();
        resultWithActiveFallback!.Value.Season.Should().BeSameAs(activeSeason);
        resultWithActiveFallback.Value.Year.Should().Be(2025);
    }

    [Fact]
    public void ResolveCurrentSeason_EmptySeasons_ReturnsNull()
    {
        // Act
        var result = SeasonDateHelper.ResolveCurrentSeason(Array.Empty<Season>(), new DateTime(2025, 6, 15));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveCurrentSeason_NoMatchingSeason_ReturnsNull()
    {
        // Arrange
        var seasons = new[]
        {
            new Season
            {
                Id = Guid.NewGuid(),
                SeasonName = "Winter-Spring",
                StartDate = "11/01",
                EndDate = "02/28",
                IsActive = true
            }
        };

        // Act
        var result = SeasonDateHelper.ResolveCurrentSeason(seasons, new DateTime(2025, 7, 15));

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
