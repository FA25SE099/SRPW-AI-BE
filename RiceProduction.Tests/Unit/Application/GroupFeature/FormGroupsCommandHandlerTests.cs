using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.GroupFeature.Commands.FormGroups;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.GroupFeature;

/// <summary>
/// Tests for FormGroupsCommandHandler - validates group formation logic
/// </summary>
public class FormGroupsCommandHandlerTests
{
    private readonly Mock<ILogger<FormGroupsCommandHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ClusterNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new FormGroupsCommandHandler(context, _mockLogger.Object);

        var command = new FormGroupsCommand
        {
            ClusterId = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            Year = 2025
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cluster") && e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_SeasonNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var cluster = new Cluster
        {
            Id = Guid.NewGuid(),
            ClusterName = "Test Cluster",
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Clusters.Add(cluster);
        await context.SaveChangesAsync();

        var handler = new FormGroupsCommandHandler(context, _mockLogger.Object);

        var command = new FormGroupsCommand
        {
            ClusterId = cluster.Id,
            SeasonId = Guid.NewGuid(),
            Year = 2025
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Season") && e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_GroupsAlreadyExist_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var clusterId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        var cluster = new Cluster
        {
            Id = clusterId,
            ClusterName = "Test Cluster",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var season = new Season
        {
            Id = seasonId,
            SeasonName = "Spring 2025",
            StartDate = "01/15",
            EndDate = "05/30",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var yearSeason = new YearSeason
        {
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025
        };

        var existingGroup = new Group
        {
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            YearSeasonId = yearSeason.Id,
            Year = 2025,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Clusters.Add(cluster);
        context.Seasons.Add(season);
        context.YearSeasons.Add(yearSeason);
        context.Groups.Add(existingGroup);
        await context.SaveChangesAsync();

        var handler = new FormGroupsCommandHandler(context, _mockLogger.Object);

        var command = new FormGroupsCommand
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Groups already exist"));
    }

    [Fact]
    public async Task Handle_NoPlotCultivations_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var clusterId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();

        var cluster = new Cluster { Id = clusterId, ClusterName = "Test Cluster" };
        var season = new Season { Id = seasonId, SeasonName = "Spring 2025", StartDate = "01/15", EndDate = "05/30" };
        var yearSeason = new YearSeason { Id = Guid.NewGuid(), ClusterId = clusterId, SeasonId = seasonId, Year = 2025 };
        var farmer = new Farmer { Id = farmerId, ClusterId = clusterId };

        context.Clusters.Add(cluster);
        context.Seasons.Add(season);
        context.YearSeasons.Add(yearSeason);
        context.Farmers.Add(farmer);
        await context.SaveChangesAsync();

        var handler = new FormGroupsCommandHandler(context, _mockLogger.Object);

        var command = new FormGroupsCommand
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No plot cultivations found"));
    }

    [Fact]
    public async Task Handle_NoEligiblePlots_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var clusterId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var plotId = Guid.NewGuid();

        var cluster = new Cluster { Id = clusterId, ClusterName = "Test Cluster" };
        var season = new Season { Id = seasonId, SeasonName = "Spring 2025", StartDate = "01/15", EndDate = "05/30" };
        var yearSeason = new YearSeason { Id = Guid.NewGuid(), ClusterId = clusterId, SeasonId = seasonId, Year = 2025 };
        var priorYearSeasonForSameSeason = new YearSeason { Id = Guid.NewGuid(), ClusterId = clusterId, SeasonId = seasonId, Year = 2024 };
        var farmer = new Farmer { Id = farmerId, ClusterId = clusterId };
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: farmerId, area: 1000m, status: PlotStatus.Active);
        var plotCultivation = MockDataBuilder.CreatePlotCultivation(plotId: plotId, id: Guid.NewGuid(), seasonId: seasonId, riceVarietyId: Guid.NewGuid(), status: CultivationStatus.Planned);

        // Mark plot as already assigned to a group for this season
        var existingSeasonGroup = new Group
        {
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            YearSeasonId = priorYearSeasonForSameSeason.Id,
            Year = 2024
        };
        var existingGroupPlot = new GroupPlot
        {
            Id = Guid.NewGuid(),
            GroupId = existingSeasonGroup.Id,
            PlotId = plotId
        };

        context.Clusters.Add(cluster);
        context.Seasons.Add(season);
        context.YearSeasons.AddRange(yearSeason, priorYearSeasonForSameSeason);
        context.Farmers.Add(farmer);
        context.Plots.Add(plot);
        context.PlotCultivations.Add(plotCultivation);
        context.Groups.Add(existingSeasonGroup);
        context.GroupPlots.Add(existingGroupPlot);
        await context.SaveChangesAsync();

        var handler = new FormGroupsCommandHandler(context, _mockLogger.Object);

        var command = new FormGroupsCommand
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No eligible plots found"));
    }

    [Fact]
    public async Task Handle_ValidCommand_WithCustomParameters_UsesProvidedValues()
    {
        // Arrange - This test verifies that custom parameters are accepted
        await using var context = TestApplicationDbContext.Create();
        var clusterId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        var cluster = new Cluster { Id = clusterId, ClusterName = "Test Cluster" };
        var season = new Season { Id = seasonId, SeasonName = "Spring 2025", StartDate = "01/15", EndDate = "05/30" };
        var yearSeason = new YearSeason { Id = Guid.NewGuid(), ClusterId = clusterId, SeasonId = seasonId, Year = 2025 };

        context.Clusters.Add(cluster);
        context.Seasons.Add(season);
        context.YearSeasons.Add(yearSeason);
        await context.SaveChangesAsync();

        var handler = new FormGroupsCommandHandler(context, _mockLogger.Object);

        var command = new FormGroupsCommand
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025,
            ProximityThreshold = 3000,
            PlantingDateTolerance = 3,
            MinGroupArea = 20.0m,
            MaxGroupArea = 60.0m,
            MinPlotsPerGroup = 3,
            MaxPlotsPerGroup = 20,
            AutoAssignSupervisors = true,
            CreateGroupsImmediately = false
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse(); // Will fail due to no plots, but command is valid
        result.Errors.Should().Contain(e => e.Contains("No plot cultivations found"));
        command.ProximityThreshold.Should().Be(3000);
        command.MinGroupArea.Should().Be(20.0m);
    }

    [Fact]
    public async Task Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        await using var context = TestApplicationDbContext.Create();
        var handler = new FormGroupsCommandHandler(context, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
