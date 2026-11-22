using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.ProductionPlanFeature.Commands.CreateProductionPlan;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.ProductionPlanFeature;

/// <summary>
/// Tests for CreateProductionPlanCommandHandler - validates production plan creation
/// </summary>
public class CreateProductionPlanCommandHandlerTests
{
    private readonly Mock<ILogger<CreateProductionPlanCommandHandler>> _mockLogger = new();

    private static List<ProductionStageRequest> CreateSampleStages()
    {
        return new List<ProductionStageRequest>
        {
            new ProductionStageRequest
            {
                StageName = "Land Preparation",
                SequenceOrder = 1,
                Description = "Prepare the land for planting",
                TypicalDurationDays = 7,
                Tasks = new List<ProductionPlanTaskRequest>
                {
                    new ProductionPlanTaskRequest
                    {
                        TaskName = "Plowing",
                        TaskType = TaskType.LandPreparation,
                        ScheduledDate = DateTime.UtcNow.AddDays(1),
                        ScheduledEndDate = DateTime.UtcNow.AddDays(2),
                        SequenceOrder = 1,
                        Materials = new List<ProductionPlanTaskMaterialRequest>()
                    }
                }
            }
        };
    }

    [Fact]
    public async Task Handle_ValidGroupBasedPlan_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        context.Groups.Add(new Group
        {
            Id = groupId,
            GroupName = "Group A",
            TotalArea = 10000m,
            Status = GroupStatus.Active
        });
        await context.SaveChangesAsync();

        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Winter-Spring Plan 2024",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        var savedPlan = await context.ProductionPlans.FirstOrDefaultAsync(p => p.Id == result.Data);
        savedPlan.Should().NotBeNull();
        savedPlan!.TotalArea.Should().Be(10000m);
    }

    [Fact]
    public async Task Handle_GroupNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            GroupId = Guid.NewGuid(),
            PlanName = "Test Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Group") && e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_ValidPlanWithTotalArea_CreatesSuccessfully()
    {
        // Arrange (no GroupId, using TotalArea directly)
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            PlanName = "Custom Area Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            TotalArea = 5000m,
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_GroupWithZeroTotalArea_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        context.Groups.Add(new Group
        {
            Id = groupId,
            GroupName = "Invalid Group",
            TotalArea = 0m,
            Status = GroupStatus.Active
        });
        await context.SaveChangesAsync();

        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Invalid Area Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Total Area") || e.Contains("zero"));
    }

    [Fact]
    public async Task Handle_PlanWithMultipleStagesAndTasks_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        context.Groups.Add(new Group
        {
            Id = groupId,
            GroupName = "Group B",
            TotalArea = 5000m,
            Status = GroupStatus.Active
        });
        await context.SaveChangesAsync();

        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var stages = new List<ProductionStageRequest>
        {
            new ProductionStageRequest
            {
                StageName = "Land Preparation",
                SequenceOrder = 1,
                Tasks = new List<ProductionPlanTaskRequest>
                {
                    new ProductionPlanTaskRequest
                    {
                        TaskName = "Plowing",
                        TaskType = TaskType.LandPreparation,
                        ScheduledDate = DateTime.UtcNow.AddDays(1),
                        SequenceOrder = 1,
                        Materials = new List<ProductionPlanTaskMaterialRequest>()
                    },
                    new ProductionPlanTaskRequest
                    {
                        TaskName = "Leveling",
                        TaskType = TaskType.LandPreparation,
                        ScheduledDate = DateTime.UtcNow.AddDays(3),
                        SequenceOrder = 2,
                        Materials = new List<ProductionPlanTaskMaterialRequest>()
                    }
                }
            },
            new ProductionStageRequest
            {
                StageName = "Planting",
                SequenceOrder = 2,
                Tasks = new List<ProductionPlanTaskRequest>
                {
                    new ProductionPlanTaskRequest
                    {
                        TaskName = "Sowing Seeds",
                        TaskType = TaskType.Sowing,
                        ScheduledDate = DateTime.UtcNow.AddDays(8),
                        SequenceOrder = 1,
                        Materials = new List<ProductionPlanTaskMaterialRequest>()
                    }
                }
            }
        };

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Plan with Multiple Stages",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = stages
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        (await context.ProductionStages.CountAsync()).Should().Be(2);
        (await context.ProductionPlanTasks.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        context.Groups.Add(new Group
        {
            Id = groupId,
            GroupName = "Group D",
            TotalArea = 3000m,
            Status = GroupStatus.Active
        });
        await context.SaveChangesAsync();
        context.SaveChangesException = new Exception("Database error");

        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Test Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }

    [Fact]
    public async Task Handle_InactiveGroup_StillCreates()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        context.Groups.Add(new Group
        {
            Id = groupId,
            GroupName = "Inactive Group",
            TotalArea = 2000m,
            Status = GroupStatus.Draft
        });
        await context.SaveChangesAsync();

        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Plan for Inactive Group",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - Assuming inactive groups can still have plans
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LargeTotalArea_HandlesCorrectly()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var groupId = Guid.NewGuid();
        context.Groups.Add(new Group
        {
            Id = groupId,
            GroupName = "Large Group",
            TotalArea = 1000000m,
            Status = GroupStatus.Active
        });
        await context.SaveChangesAsync();

        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Large Scale Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PlanWithStandardPlanId_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreateProductionPlanCommandHandler(context, _mockLogger.Object);

        var command = new CreateProductionPlanCommand
        {
            StandardPlanId = Guid.NewGuid(),
            PlanName = "Plan from Standard",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            TotalArea = 4000m,
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
