using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.UAVFeature.Commands.CreateUavOrder;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.UAVFeature;

/// <summary>
/// Tests for CreateUavOrderCommandHandler - validates UAV service order creation
/// </summary>
public class CreateUavOrderCommandHandlerTests
{
    private readonly Mock<ILogger<CreateUavOrderCommandHandler>> _mockLogger;

    public CreateUavOrderCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<CreateUavOrderCommandHandler>>();
    }

    [Fact]
    public async Task Handle_MissingClusterManagerId_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreateUavOrderCommandHandler(context, _mockLogger.Object);

        var command = new CreateUavOrderCommand
        {
            GroupId = Guid.NewGuid(),
            UavVendorId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = null // Missing cluster manager
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cluster Manager ID is required.");
    }

    [Fact]
    public async Task Handle_GroupNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreateUavOrderCommandHandler(context, _mockLogger.Object);

        var command = new CreateUavOrderCommand
        {
            GroupId = Guid.NewGuid(),
            UavVendorId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Group not found.");
    }

    [Fact]
    public async Task Handle_VendorNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.TotalArea = 5000M;

        context.Groups.Add(group);
        await context.SaveChangesAsync();

        var handler = new CreateUavOrderCommandHandler(context, _mockLogger.Object);

        var command = new CreateUavOrderCommand
        {
            GroupId = groupId,
            UavVendorId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("UAV Vendor not found.");
    }

    [Fact]
    public async Task Handle_InvalidGroupArea_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.TotalArea = 0M; // Invalid area

        var vendor = new UavVendor
        {
            Id = Guid.NewGuid(),
            VendorName = "Test Vendor",
            ServiceRatePerHa = 500000M
        };

        context.Groups.Add(group);
        context.UavVendors.Add(vendor);
        await context.SaveChangesAsync();

        var handler = new CreateUavOrderCommandHandler(context, _mockLogger.Object);

        var command = new CreateUavOrderCommand
        {
            GroupId = groupId,
            UavVendorId = vendor.Id,
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { Guid.NewGuid() },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Group area is invalid.");
    }

    [Fact]
    public async Task Handle_NoActiveTasks_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
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

        context.Plots.Add(plot);
        context.PlotCultivations.Add(plotCultivation);
        context.Groups.Add(group);
        context.GroupPlots.Add(groupPlot);
        context.UavVendors.Add(vendor);
        await context.SaveChangesAsync();

        var handler = new CreateUavOrderCommandHandler(context, _mockLogger.Object);

        var command = new CreateUavOrderCommand
        {
            GroupId = groupId,
            UavVendorId = vendor.Id,
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            SelectedPlotIds = new List<Guid> { plotId },
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("No active Cultivation Tasks found for the selected plots.");
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesUavOrderSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
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

        context.Clusters.Add(cluster);
        context.Plots.Add(plot);
        context.PlotCultivations.Add(plotCultivation);
        context.Groups.Add(group);
        context.GroupPlots.Add(groupPlot);
        context.CultivationTasks.Add(cultivationTask);
        context.UavVendors.Add(vendor);
        await context.SaveChangesAsync();

        var handler = new CreateUavOrderCommandHandler(context, _mockLogger.Object);

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
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue($"Expected success but got errors: {string.Join(", ", result.Errors)}");
        result.Data.Should().NotBe(Guid.Empty);

        var savedOrder = await context.UavServiceOrders
            .Include(o => o.PlotAssignments)
            .FirstOrDefaultAsync(o => o.Id == result.Data);
        savedOrder.Should().NotBeNull();
        savedOrder!.GroupId.Should().Be(groupId);
        savedOrder.UavVendorId.Should().Be(vendor.Id);
        savedOrder.Priority.Should().Be(TaskPriority.High);
        savedOrder.PlotAssignments.Should().ContainSingle(a => a.PlotId == plotId && a.CultivationTaskId == cultivationTaskId);

        var updatedTask = await context.CultivationTasks.FirstOrDefaultAsync(t => t.Id == cultivationTaskId);
        updatedTask!.AssignedToVendorId.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreateUavOrderCommandHandler(context, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
