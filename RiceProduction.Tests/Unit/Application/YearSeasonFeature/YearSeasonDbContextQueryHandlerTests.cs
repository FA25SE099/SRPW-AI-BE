using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.YearSeasonFeature.Queries.GetActiveYearSeasons;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonsByCluster;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;

namespace RiceProduction.Tests.Unit.Application.YearSeasonFeature;

/// <summary>
/// Real EF Core InMemory tests exercising <see cref="RiceProduction.Application.Common.Interfaces.IApplicationDbContext"/>
/// query handlers end-to-end without repository mocking.
/// </summary>
public class YearSeasonDbContextQueryHandlerTests
{
    [Fact]
    public async Task GetYearSeasonsByCluster_CategorizesPastCurrentAndUpcomingSeasonsWithGroupCounts()
    {
        await using var context = TestApplicationDbContext.Create();
        var cluster = new Cluster { Id = Guid.NewGuid(), ClusterName = "Mekong Cluster A" };
        var season = new Season { Id = Guid.NewGuid(), SeasonName = "Winter-Spring", StartDate = "11/01", EndDate = "02/28" };
        var variety = new RiceVariety { Id = Guid.NewGuid(), VarietyName = "OM 5451" };
        var now = DateTime.UtcNow;

        var pastYs = new YearSeason
        {
            Id = Guid.NewGuid(),
            ClusterId = cluster.Id,
            SeasonId = season.Id,
            RiceVarietyId = variety.Id,
            Year = now.Year - 1,
            StartDate = now.AddMonths(-8),
            EndDate = now.AddMonths(-4),
            Status = SeasonStatus.Completed
        };

        var currentYs = new YearSeason
        {
            Id = Guid.NewGuid(),
            ClusterId = cluster.Id,
            SeasonId = season.Id,
            RiceVarietyId = variety.Id,
            Year = now.Year,
            StartDate = now.AddDays(-15),
            EndDate = now.AddDays(45),
            Status = SeasonStatus.Active
        };

        var upcomingYs = new YearSeason
        {
            Id = Guid.NewGuid(),
            ClusterId = cluster.Id,
            SeasonId = season.Id,
            RiceVarietyId = variety.Id,
            Year = now.Year + 1,
            StartDate = now.AddMonths(3),
            EndDate = now.AddMonths(7),
            Status = SeasonStatus.Draft
        };

        var group1 = new Group { Id = Guid.NewGuid(), ClusterId = cluster.Id, YearSeasonId = currentYs.Id, Year = now.Year };
        var group2 = new Group { Id = Guid.NewGuid(), ClusterId = cluster.Id, YearSeasonId = currentYs.Id, Year = now.Year };

        context.Clusters.Add(cluster);
        context.Seasons.Add(season);
        context.RiceVarieties.Add(variety);
        context.YearSeasons.AddRange(pastYs, currentYs, upcomingYs);
        context.Groups.AddRange(group1, group2);
        await context.SaveChangesAsync();

        var handler = new GetYearSeasonsByClusterQueryHandler(
            context,
            Mock.Of<ILogger<GetYearSeasonsByClusterQueryHandler>>());

        var result = await handler.Handle(new GetYearSeasonsByClusterQuery { ClusterId = cluster.Id }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ClusterName.Should().Be("Mekong Cluster A");
        result.Data.AllSeasons.Should().HaveCount(3);
        result.Data.CurrentSeason.Should().NotBeNull();
        result.Data.CurrentSeason!.Id.Should().Be(currentYs.Id);
        result.Data.CurrentSeason.GroupCount.Should().Be(2);
        result.Data.CurrentSeason.RiceVarietyName.Should().Be("OM 5451");
        result.Data.PastSeasons.Should().ContainSingle(s => s.Id == pastYs.Id);
        result.Data.UpcomingSeasons.Should().ContainSingle(s => s.Id == upcomingYs.Id);
    }

    [Fact]
    public async Task GetYearSeasonsByCluster_MissingCluster_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = new GetYearSeasonsByClusterQueryHandler(
            context,
            Mock.Of<ILogger<GetYearSeasonsByClusterQueryHandler>>());

        var result = await handler.Handle(new GetYearSeasonsByClusterQuery { ClusterId = Guid.NewGuid() }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetActiveYearSeasons_FiltersOnlyOpenFarmerSelectionWindows()
    {
        await using var context = TestApplicationDbContext.Create();
        var cluster = new Cluster { Id = Guid.NewGuid(), ClusterName = "Delta Cluster" };
        var season = new Season { Id = Guid.NewGuid(), SeasonName = "Summer-Autumn", StartDate = "04/01", EndDate = "07/31" };
        var now = DateTime.UtcNow;

        var openSelection = new YearSeason
        {
            Id = Guid.NewGuid(),
            ClusterId = cluster.Id,
            SeasonId = season.Id,
            Year = now.Year,
            StartDate = now.AddDays(10),
            EndDate = now.AddDays(110),
            Status = SeasonStatus.PlanningOpen,
            AllowFarmerSelection = true,
            FarmerSelectionWindowStart = now.AddDays(-2),
            FarmerSelectionWindowEnd = now.AddDays(5)
        };

        var closedSelection = new YearSeason
        {
            Id = Guid.NewGuid(),
            ClusterId = cluster.Id,
            SeasonId = season.Id,
            Year = now.Year,
            StartDate = now.AddDays(10),
            EndDate = now.AddDays(110),
            Status = SeasonStatus.PlanningOpen,
            AllowFarmerSelection = false,
            FarmerSelectionWindowStart = now.AddDays(-2),
            FarmerSelectionWindowEnd = now.AddDays(5)
        };

        var expiredWindow = new YearSeason
        {
            Id = Guid.NewGuid(),
            ClusterId = cluster.Id,
            SeasonId = season.Id,
            Year = now.Year,
            StartDate = now.AddDays(10),
            EndDate = now.AddDays(110),
            Status = SeasonStatus.PlanningOpen,
            AllowFarmerSelection = true,
            FarmerSelectionWindowStart = now.AddDays(-10),
            FarmerSelectionWindowEnd = now.AddDays(-1)
        };

        context.Clusters.Add(cluster);
        context.Seasons.Add(season);
        context.YearSeasons.AddRange(openSelection, closedSelection, expiredWindow);
        await context.SaveChangesAsync();

        var handler = new GetActiveYearSeasonsQueryHandler(
            context,
            Mock.Of<ILogger<GetActiveYearSeasonsQueryHandler>>());

        var result = await handler.Handle(new GetActiveYearSeasonsQuery { ClusterId = cluster.Id }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.ActiveSeasons.Should().ContainSingle(s => s.Id == openSelection.Id);
    }
}
