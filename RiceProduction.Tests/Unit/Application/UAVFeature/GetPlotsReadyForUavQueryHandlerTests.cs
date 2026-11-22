using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.UAVFeature;

/// <summary>
/// Tests for GetPlotsReadyForUavQueryHandler - validates task-level UAV assignment tracking
/// This is critical business logic that changed from plot-level to task-level tracking
/// </summary>
public class GetPlotsReadyForUavQueryHandlerTests
{
    private readonly Mock<ILogger<GetPlotsReadyForUavQueryHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_GroupNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new GetPlotsReadyForUavQueryHandler(context, _mockLogger.Object);

        var query = new GetPlotsReadyForUavQuery 
        { 
            GroupId = Guid.NewGuid(),
            RequiredTaskType = TaskType.PestControl,
            DaysBeforeScheduled = 7
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Group not found.");
    }

    [Fact]
    public async Task Handle_EmptyGroup_ReturnsSuccessWithEmptyList()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.GroupPlots = new List<GroupPlot>(); // Empty group

        context.Groups.Add(group);
        await context.SaveChangesAsync();

        var handler = new GetPlotsReadyForUavQueryHandler(context, _mockLogger.Object);

        var query = new GetPlotsReadyForUavQuery
        {
            GroupId = groupId,
            RequiredTaskType = TaskType.PestControl,
            DaysBeforeScheduled = 7
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
        result.Message.Should().Contain("No plots found");
    }

    [Fact]
    public async Task Handle_PlotWithReadyTask_ReturnsSuccessWithReadyPlot()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var plotCultivationId = Guid.NewGuid();
        var cultivationTaskId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var riceVarietyId = Guid.NewGuid();

        // Create entities
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000M, status: PlotStatus.Active);
        var plotCultivation = MockDataBuilder.CreatePlotCultivation(
            id: plotCultivationId, 
            plotId: plotId, 
            seasonId: seasonId, 
            riceVarietyId: riceVarietyId, 
            status: CultivationStatus.InProgress);
        plotCultivation.Area = 1000M;
        plot.PlotCultivations = new List<PlotCultivation> { plotCultivation };

        var cultivationVersion = new CultivationVersion
        {
            Id = versionId,
            PlotCultivationId = plotCultivationId,
            VersionName = "v1",
            VersionOrder = 1,
            IsActive = true
        };

        var groupPlot = new GroupPlot 
        { 
            Id = Guid.NewGuid(), 
            GroupId = groupId, 
            PlotId = plotId, 
            Plot = plot 
        };

        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.GroupPlots = new List<GroupPlot> { groupPlot };

        // Create a task scheduled within the next 7 days (ready for UAV)
        var scheduledDate = DateTime.UtcNow.AddDays(5);
        var cultivationTask = MockDataBuilder.CreateCultivationTask(
            id: cultivationTaskId,
            executionOrder: 1,
            status: RiceProduction.Domain.Enums.TaskStatus.Draft,
            plotCultivationId: plotCultivationId,
            taskType: TaskType.PestControl,
            scheduledEndDate: scheduledDate);
        cultivationTask.PlotCultivation = plotCultivation;
        cultivationTask.VersionId = versionId;
        cultivationTask.Version = cultivationVersion;
        cultivationTask.ProductionPlanTask = new ProductionPlanTask
        {
            Id = Guid.NewGuid(),
            TaskName = "Pest Control Task",
            ScheduledDate = scheduledDate,
            ProductionPlanTaskMaterials = new List<ProductionPlanTaskMaterial>
            {
                new ProductionPlanTaskMaterial 
                { 
                    Id = Guid.NewGuid(), 
                    EstimatedAmount = 50000M 
                }
            }
        };

        context.Groups.Add(group);
        context.CultivationVersions.Add(cultivationVersion);
        context.CultivationTasks.Add(cultivationTask);
        await context.SaveChangesAsync();

        var handler = new GetPlotsReadyForUavQueryHandler(context, _mockLogger.Object);

        var query = new GetPlotsReadyForUavQuery
        {
            GroupId = groupId,
            RequiredTaskType = TaskType.PestControl,
            DaysBeforeScheduled = 7
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(1);
        
        var readyPlot = result.Data.First();
        readyPlot.PlotId.Should().Be(plotId);
        readyPlot.CultivationTaskId.Should().Be(cultivationTaskId);
        readyPlot.TaskType.Should().Be(TaskType.PestControl);
        readyPlot.IsReady.Should().BeTrue();
        readyPlot.HasActiveUavOrder.Should().BeFalse();
        readyPlot.PlotArea.Should().Be(1000M);
        readyPlot.EstimatedMaterialCost.Should().Be(50000M);
    }

    [Fact]
    public async Task Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        await using var context = TestApplicationDbContext.Create();
        var handler = new GetPlotsReadyForUavQueryHandler(context, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
