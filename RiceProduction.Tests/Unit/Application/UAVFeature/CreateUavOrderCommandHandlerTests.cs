using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.UAVFeature.Commands.CreateUavOrder;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using System.Linq.Expressions;
using Xunit;
using Microsoft.EntityFrameworkCore.Query;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Tests.Unit.Application.UAVFeature;

/// <summary>
/// Tests for CreateUavOrderCommandHandler - validates UAV service order creation
/// </summary>
public class CreateUavOrderCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Group>> _mockGroupRepo;
    private readonly Mock<IGenericRepository<CultivationTask>> _mockCultivationTaskRepo;
    private readonly Mock<IGenericRepository<UavServiceOrder>> _mockUavOrderRepo;
    private readonly Mock<IUavVendorRepository> _mockUavVendorRepo;
    private readonly Mock<ILogger<CreateUavOrderCommandHandler>> _mockLogger;
    private readonly CreateUavOrderCommandHandler _handler;

    public CreateUavOrderCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockGroupRepo = new Mock<IGenericRepository<Group>>();
        _mockCultivationTaskRepo = new Mock<IGenericRepository<CultivationTask>>();
        _mockUavOrderRepo = new Mock<IGenericRepository<UavServiceOrder>>();
        _mockUavVendorRepo = new Mock<IUavVendorRepository>();
        _mockLogger = new Mock<ILogger<CreateUavOrderCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Group>()).Returns(_mockGroupRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<CultivationTask>()).Returns(_mockCultivationTaskRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<UavServiceOrder>()).Returns(_mockUavOrderRepo.Object);
        _mockUnitOfWork.Setup(u => u.UavVendorRepository).Returns(_mockUavVendorRepo.Object);

        _handler = new CreateUavOrderCommandHandler(_mockUnitOfWork.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_MissingClusterManagerId_ReturnsFailure()
    {
        // Arrange
        var command = new CreateUavOrderCommand
        {
            GroupId = Guid.NewGuid(),
            UavVendorId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = null // Missing cluster manager
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cluster Manager ID is required.");
    }

    [Fact]
    public async Task Handle_GroupNotFound_ReturnsFailure()
    {
        // Arrange
        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync((Group?)null);

        var command = new CreateUavOrderCommand
        {
            GroupId = Guid.NewGuid(),
            UavVendorId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Group not found.");
    }

    [Fact]
    public async Task Handle_VendorNotFound_ReturnsFailure()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.TotalArea = 5000M;

        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync(group);

        _mockUavVendorRepo.Setup(r => r.GetUavVendorByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UavVendor?)null);

        var command = new CreateUavOrderCommand
        {
            GroupId = groupId,
            UavVendorId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("UAV Vendor not found.");
    }

    [Fact]
    public async Task Handle_InvalidGroupArea_ReturnsFailure()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.TotalArea = 0M; // Invalid area

        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync(group);

        var vendor = new UavVendor
        {
            Id = Guid.NewGuid(),
            VendorName = "Test Vendor",
            ServiceRatePerHa = 500000M
        };

        _mockUavVendorRepo.Setup(r => r.GetUavVendorByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var command = new CreateUavOrderCommand
        {
            GroupId = groupId,
            UavVendorId = vendor.Id,
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Group area is invalid.");
    }

    [Fact]
    public async Task Handle_NoActiveTasks_ReturnsFailure()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var plotCultivationId = Guid.NewGuid();

        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000M, status: PlotStatus.Active);
        var plotCultivation = MockDataBuilder.CreatePlotCultivation(
            id: plotCultivationId,
            plotId: plotId,
            seasonId: Guid.NewGuid(),
            riceVarietyId: Guid.NewGuid(),
            status: CultivationStatus.InProgress);
        plot.PlotCultivations = new List<PlotCultivation> { plotCultivation };

        var groupPlot = new GroupPlot
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            PlotId = plotId,
            Plot = plot
        };

        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.TotalArea = 1000M;
        group.GroupPlots = new List<GroupPlot> { groupPlot };

        var vendor = new UavVendor
        {
            Id = Guid.NewGuid(),
            VendorName = "Test Vendor",
            ServiceRatePerHa = 500000M
        };

        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync(group);

        _mockUavVendorRepo.Setup(r => r.GetUavVendorByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        // No active tasks
        _mockCultivationTaskRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<CultivationTask, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<CultivationTask>());

        var command = new CreateUavOrderCommand
        {
            GroupId = groupId,
            UavVendorId = vendor.Id,
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { plotId },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("No active Cultivation Tasks found for the selected plots.");
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesUavOrderSuccessfully()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var plotCultivationId = Guid.NewGuid();
        var cultivationTaskId = Guid.NewGuid();
        var clusterManagerId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();

        var cluster = new Cluster
        {
            Id = clusterId,
            ClusterName = "Test Cluster",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000M, status: PlotStatus.Active);
        plot.Coordinate = new NetTopologySuite.Geometries.Point(105.123, 10.456) { SRID = 4326 };
        
        var plotCultivation = MockDataBuilder.CreatePlotCultivation(
            id: plotCultivationId,
            plotId: plotId,
            seasonId: Guid.NewGuid(),
            riceVarietyId: Guid.NewGuid(),
            status: CultivationStatus.InProgress);
        plotCultivation.Area = 1000M;
        
        plot.PlotCultivations = new List<PlotCultivation> { plotCultivation };

        var groupPlot = new GroupPlot
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            PlotId = plotId,
            Plot = plot
        };

        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.ClusterId = clusterId;
        group.Cluster = cluster;
        group.TotalArea = 1000M;
        group.GroupPlots = new List<GroupPlot> { groupPlot };

        var cultivationTask = MockDataBuilder.CreateCultivationTask(
            id: cultivationTaskId,
            executionOrder: 1,
            status: RiceProduction.Domain.Enums.TaskStatus.Draft,
            plotCultivationId: plotCultivationId,
            taskType: TaskType.PestControl,
            scheduledEndDate: DateTime.UtcNow.AddDays(5));
        cultivationTask.PlotCultivation = plotCultivation;

        var vendor = new UavVendor
        {
            Id = Guid.NewGuid(),
            VendorName = "Test Vendor",
            ServiceRatePerHa = 500000M
        };

        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync(group);

        _mockUavVendorRepo.Setup(r => r.GetUavVendorByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _mockCultivationTaskRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<CultivationTask, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<CultivationTask> { cultivationTask });

        var savedOrderId = Guid.Empty;
        _mockUavOrderRepo.Setup(r => r.AddAsync(It.IsAny<UavServiceOrder>()))
            .Callback<UavServiceOrder>(order => 
            {
                order.Id = Guid.NewGuid();
                savedOrderId = order.Id;
            })
            .Returns(Task.CompletedTask);
        
        _mockUavOrderRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var command = new CreateUavOrderCommand
        {
            GroupId = groupId,
            UavVendorId = vendor.Id,
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { plotId },
            ClusterManagerId = clusterManagerId,
            Priority = TaskPriority.High
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        if (!result.Succeeded)
        {
            Console.WriteLine($"CreateUavOrder failed with errors: {string.Join(", ", result.Errors)}");
        }
        else
        {
            Console.WriteLine($"CreateUavOrder succeeded with ID: {result.Data}");
        }
        result.Succeeded.Should().BeTrue($"Expected success but got errors: {string.Join(", ", result.Errors)}");
        result.Data.Should().NotBe(Guid.Empty);
        
        _mockUavOrderRepo.Verify(r => r.AddAsync(It.Is<UavServiceOrder>(
            o => o.GroupId == groupId && 
                 o.UavVendorId == vendor.Id &&
                 o.Priority == TaskPriority.High)), 
            Times.Once);
        
        _mockUavOrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public void Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var handler = new CreateUavOrderCommandHandler(_mockUnitOfWork.Object, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
