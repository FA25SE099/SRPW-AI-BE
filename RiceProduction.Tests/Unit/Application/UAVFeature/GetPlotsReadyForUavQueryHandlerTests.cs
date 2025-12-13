using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using System.Linq.Expressions;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.UAVFeature;

/// <summary>
/// Tests for GetPlotsReadyForUavQueryHandler - validates task-level UAV assignment tracking
/// This is critical business logic that changed from plot-level to task-level tracking
/// </summary>
public class GetPlotsReadyForUavQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Group>> _mockGroupRepo;
    private readonly Mock<ILogger<GetPlotsReadyForUavQueryHandler>> _mockLogger;
    private readonly GetPlotsReadyForUavQueryHandler _handler;

    public GetPlotsReadyForUavQueryHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockGroupRepo = new Mock<IGenericRepository<Group>>();
        _mockLogger = new Mock<ILogger<GetPlotsReadyForUavQueryHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Group>()).Returns(_mockGroupRepo.Object);

        _handler = new GetPlotsReadyForUavQueryHandler(_mockUnitOfWork.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_GroupNotFound_ReturnsFailure()
    {
        // Arrange
        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync((Group?)null);

        var query = new GetPlotsReadyForUavQuery 
        { 
            GroupId = Guid.NewGuid(),
            RequiredTaskType = TaskType.PestControl,
            DaysBeforeScheduled = 7
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Group not found.");
    }

    [Fact]
    public async Task Handle_EmptyGroup_ReturnsSuccessWithEmptyList()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = MockDataBuilder.CreateGroup(id: groupId);
        group.GroupPlots = new List<GroupPlot>(); // Empty group

        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync(group);

        var query = new GetPlotsReadyForUavQuery
        {
            GroupId = groupId,
            RequiredTaskType = TaskType.PestControl,
            DaysBeforeScheduled = 7
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

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
        var groupId = Guid.NewGuid();
        var plotId = Guid.NewGuid();
        var plotCultivationId = Guid.NewGuid();
        var cultivationTaskId = Guid.NewGuid();
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

        // Setup mocks
        var mockCultivationTaskRepo = new Mock<IGenericRepository<CultivationTask>>();
        var mockUavAssignmentRepo = new Mock<IGenericRepository<UavOrderPlotAssignment>>();

        _mockGroupRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<Group, bool>>>(),
            It.IsAny<Func<IQueryable<Group>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Group, object>>>()))
            .ReturnsAsync(group);

        mockUavAssignmentRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<UavOrderPlotAssignment, bool>>>(),
            null,
            null))
            .ReturnsAsync(new List<UavOrderPlotAssignment>()); // No active UAV orders

        mockCultivationTaskRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<CultivationTask, bool>>>(),
            null,
            It.IsAny<Func<IQueryable<CultivationTask>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<CultivationTask, object>>>()))
            .ReturnsAsync(new List<CultivationTask> { cultivationTask });

        _mockUnitOfWork.Setup(u => u.Repository<CultivationTask>()).Returns(mockCultivationTaskRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<UavOrderPlotAssignment>()).Returns(mockUavAssignmentRepo.Object);

        var query = new GetPlotsReadyForUavQuery
        {
            GroupId = groupId,
            RequiredTaskType = TaskType.PestControl,
            DaysBeforeScheduled = 7
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

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
        var handler = new GetPlotsReadyForUavQueryHandler(_mockUnitOfWork.Object, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
