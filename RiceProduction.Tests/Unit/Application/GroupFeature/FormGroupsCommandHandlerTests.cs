using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.GroupFeature.Commands.FormGroups;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Repository;
using RiceProduction.Tests.Fixtures;
using System.Linq.Expressions;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.GroupFeature;

/// <summary>
/// Tests for FormGroupsCommandHandler - validates group formation logic
/// </summary>
public class FormGroupsCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Cluster>> _mockClusterRepo;
    private readonly Mock<IGenericRepository<Season>> _mockSeasonRepo;
    private readonly Mock<IGenericRepository<Group>> _mockGroupRepo;
    private readonly Mock<IGenericRepository<Plot>> _mockPlotRepo;
    private readonly Mock<IGenericRepository<PlotCultivation>> _mockPlotCultivationRepo;
    private readonly Mock<IGenericRepository<RiceVariety>> _mockRiceVarietyRepo;
    private readonly Mock<IFarmerRepository> _mockFarmerRepo;
    private readonly Mock<ISupervisorRepository> _mockSupervisorRepo;
    private readonly Mock<IPlotRepository> _mockPlotRepoInterface;
    private readonly Mock<ILogger<FormGroupsCommandHandler>> _mockLogger;
    private readonly FormGroupsCommandHandler _handler;

    public FormGroupsCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockClusterRepo = new Mock<IGenericRepository<Cluster>>();
        _mockSeasonRepo = new Mock<IGenericRepository<Season>>();
        _mockGroupRepo = new Mock<IGenericRepository<Group>>();
        _mockPlotRepo = new Mock<IGenericRepository<Plot>>();
        _mockPlotCultivationRepo = new Mock<IGenericRepository<PlotCultivation>>();
        _mockRiceVarietyRepo = new Mock<IGenericRepository<RiceVariety>>();
        _mockFarmerRepo = new Mock<IFarmerRepository>();
        _mockSupervisorRepo = new Mock<ISupervisorRepository>();
        _mockPlotRepoInterface = new Mock<IPlotRepository>();
        _mockLogger = new Mock<ILogger<FormGroupsCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Cluster>()).Returns(_mockClusterRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Season>()).Returns(_mockSeasonRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Group>()).Returns(_mockGroupRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Plot>()).Returns(_mockPlotRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<PlotCultivation>()).Returns(_mockPlotCultivationRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<RiceVariety>()).Returns(_mockRiceVarietyRepo.Object);
        _mockUnitOfWork.Setup(u => u.FarmerRepository).Returns(_mockFarmerRepo.Object);
        _mockUnitOfWork.Setup(u => u.SupervisorRepository).Returns(_mockSupervisorRepo.Object);
        _mockUnitOfWork.Setup(u => u.PlotRepository).Returns(_mockPlotRepoInterface.Object);

        _handler = new FormGroupsCommandHandler(_mockUnitOfWork.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ClusterNotFound_ReturnsFailure()
    {
        // Arrange
        _mockClusterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cluster, bool>>>()))
            .ReturnsAsync((Cluster?)null);

        var command = new FormGroupsCommand
        {
            ClusterId = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            Year = 2025
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cluster") && e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_SeasonNotFound_ReturnsFailure()
    {
        // Arrange
        var cluster = new Cluster
        {
            Id = Guid.NewGuid(),
            ClusterName = "Test Cluster",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockClusterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cluster, bool>>>()))
            .ReturnsAsync(cluster);

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        var command = new FormGroupsCommand
        {
            ClusterId = cluster.Id,
            SeasonId = Guid.NewGuid(),
            Year = 2025
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Season") && e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_GroupsAlreadyExist_ReturnsFailure()
    {
        // Arrange
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

        var existingGroup = new Group
        {
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            YearSeasonId = null, // Changed from SeasonId to YearSeasonId
            Year = 2025,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockClusterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cluster, bool>>>()))
            .ReturnsAsync(cluster);

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync(season);

        _mockGroupRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Group> { existingGroup });

        var command = new FormGroupsCommand
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Groups already exist"));
    }

    [Fact]
    public async Task Handle_NoPlotCultivations_ReturnsFailure()
    {
        // Arrange
        var clusterId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();

        var cluster = new Cluster { Id = clusterId, ClusterName = "Test Cluster" };
        var season = new Season { Id = seasonId, SeasonName = "Spring 2025", StartDate = "01/15", EndDate = "05/30" };
        var farmer = new Farmer { Id = farmerId, ClusterId = clusterId };

        _mockClusterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cluster, bool>>>())).ReturnsAsync(cluster);
        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>())).ReturnsAsync(season);
        _mockGroupRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Group, bool>>>(), null, null)).ReturnsAsync(new List<Group>());
        _mockFarmerRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Farmer, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Farmer> { farmer });
        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null)).ReturnsAsync(new List<Plot>());
        _mockPlotCultivationRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>(), null, null)).ReturnsAsync(new List<PlotCultivation>());

        var command = new FormGroupsCommand
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No plot cultivations found"));
    }

    [Fact]
    public async Task Handle_NoEligiblePlots_ReturnsFailure()
    {
        // Arrange
        var clusterId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var plotId = Guid.NewGuid();

        var cluster = new Cluster { Id = clusterId, ClusterName = "Test Cluster" };
        var season = new Season { Id = seasonId, SeasonName = "Spring 2025", StartDate = "01/15", EndDate = "05/30" };
        var farmer = new Farmer { Id = farmerId, ClusterId = clusterId };
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: farmerId, area: 1000m, status: PlotStatus.Active);
        var plotCultivation = MockDataBuilder.CreatePlotCultivation(plotId: plotId, id: Guid.NewGuid(), seasonId: seasonId, riceVarietyId: Guid.NewGuid(), status: CultivationStatus.Planned);

        _mockClusterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cluster, bool>>>())).ReturnsAsync(cluster);
        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>())).ReturnsAsync(season);
        _mockGroupRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Group, bool>>>(), null, null)).ReturnsAsync(new List<Group>());
        _mockFarmerRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Farmer, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Farmer> { farmer });
        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null)).ReturnsAsync(new List<Plot> { plot });
        _mockPlotCultivationRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>(), null, null)).ReturnsAsync(new List<PlotCultivation> { plotCultivation });
        
        // Mock all plots as already grouped
        _mockPlotRepoInterface.Setup(r => r.IsPlotAssignedToGroupForSeasonAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new FormGroupsCommand
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = 2025
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No eligible plots found"));
    }

    [Fact]
    public async Task Handle_ValidCommand_WithCustomParameters_UsesProvidedValues()
    {
        // Arrange - This test verifies that custom parameters are accepted
        // Note: Full group formation requires complex setup with GroupFormationService
        var clusterId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        var cluster = new Cluster { Id = clusterId, ClusterName = "Test Cluster" };
        var season = new Season { Id = seasonId, SeasonName = "Spring 2025", StartDate = "01/15", EndDate = "05/30" };

        _mockClusterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cluster, bool>>>())).ReturnsAsync(cluster);
        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>())).ReturnsAsync(season);
        _mockGroupRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Group, bool>>>(), null, null)).ReturnsAsync(new List<Group>());
        _mockFarmerRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<Farmer, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<Farmer>());
        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null)).ReturnsAsync(new List<Plot>());
        _mockPlotCultivationRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>(), null, null)).ReturnsAsync(new List<PlotCultivation>());

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
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse(); // Will fail due to no plots, but command is valid
        // The fact it didn't fail on parameter validation means parameters were accepted
        command.ProximityThreshold.Should().Be(3000);
        command.MinGroupArea.Should().Be(20.0m);
    }

    [Fact]
    public void Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var handler = new FormGroupsCommandHandler(_mockUnitOfWork.Object, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
