using FluentAssertions;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.ProductionPlanFeature.Commands.CreateProductionPlan;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.ProductionPlanFeature;

/// <summary>
/// Tests for CreateProductionPlanCommandHandler - validates production plan creation
/// </summary>
public class CreateProductionPlanCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<ILogger<CreateProductionPlanCommandHandler>> _mockLogger;
    private readonly Mock<IGenericRepository<ProductionPlan>> _mockProductionPlanRepo;
    private readonly Mock<IGenericRepository<ProductionStage>> _mockProductionStageRepo;
    private readonly Mock<IGenericRepository<ProductionPlanTask>> _mockProductionPlanTaskRepo;
    private readonly Mock<IGenericRepository<Group>> _mockGroupRepo;
    private readonly Mock<IGenericRepository<Material>> _mockMaterialRepo;
    private readonly Mock<IGenericRepository<MaterialPrice>> _mockMaterialPriceRepo;
    private readonly CreateProductionPlanCommandHandler _handler;

    public CreateProductionPlanCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _mockLogger = new Mock<ILogger<CreateProductionPlanCommandHandler>>();
        _mockProductionPlanRepo = new Mock<IGenericRepository<ProductionPlan>>();
        _mockProductionStageRepo = new Mock<IGenericRepository<ProductionStage>>();
        _mockProductionPlanTaskRepo = new Mock<IGenericRepository<ProductionPlanTask>>();
        _mockGroupRepo = new Mock<IGenericRepository<Group>>();
        _mockMaterialRepo = new Mock<IGenericRepository<Material>>();
        _mockMaterialPriceRepo = new Mock<IGenericRepository<MaterialPrice>>();

        _mockUnitOfWork.Setup(u => u.Repository<ProductionPlan>()).Returns(_mockProductionPlanRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<ProductionStage>()).Returns(_mockProductionStageRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<ProductionPlanTask>()).Returns(_mockProductionPlanTaskRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Group>()).Returns(_mockGroupRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Material>()).Returns(_mockMaterialRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<MaterialPrice>()).Returns(_mockMaterialPriceRepo.Object);

        _handler = new CreateProductionPlanCommandHandler(
            _mockUnitOfWork.Object,
            _mockMapper.Object,
            _mockLogger.Object);
    }

    private List<ProductionStageRequest> CreateSampleStages()
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
        var groupId = Guid.NewGuid();
        var group = new Group
        {
            Id = groupId,
            GroupName = "Group A",
            TotalArea = 10000m,
            Status = GroupStatus.Active
        };

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Winter-Spring Plan 2024",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync(group);

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null, null))
            .ReturnsAsync(new List<MaterialPrice>());

        _mockProductionPlanRepo.Setup(r => r.AddAsync(It.IsAny<ProductionPlan>()))
            .Returns(Task.CompletedTask);

        _mockProductionStageRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionStage>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanTaskRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionPlanTask>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        _mockProductionPlanRepo.Verify(r => r.AddAsync(It.IsAny<ProductionPlan>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GroupNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new CreateProductionPlanCommand
        {
            GroupId = Guid.NewGuid(),
            PlanName = "Test Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync((Group?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Group") && e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_ValidPlanWithTotalArea_CreatesSuccessfully()
    {
        // Arrange (no GroupId, using TotalArea directly)
        var command = new CreateProductionPlanCommand
        {
            PlanName = "Custom Area Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            TotalArea = 5000m,
            Stages = CreateSampleStages()
        };

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null, null))
            .ReturnsAsync(new List<MaterialPrice>());

        _mockProductionPlanRepo.Setup(r => r.AddAsync(It.IsAny<ProductionPlan>()))
            .Returns(Task.CompletedTask);

        _mockProductionStageRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionStage>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanTaskRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionPlanTask>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_GroupWithZeroTotalArea_ReturnsFailure()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = new Group
        {
            Id = groupId,
            GroupName = "Invalid Group",
            TotalArea = 0m,
            Status = GroupStatus.Active
        };

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Invalid Area Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync(group);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Total Area") || e.Contains("zero"));
    }

    [Fact]
    public async Task Handle_PlanWithMultipleStagesAndTasks_CreatesSuccessfully()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = new Group
        {
            Id = groupId,
            GroupName = "Group B",
            TotalArea = 5000m,
            Status = GroupStatus.Active
        };

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

        _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync(group);

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null, null))
            .ReturnsAsync(new List<MaterialPrice>());

        _mockProductionPlanRepo.Setup(r => r.AddAsync(It.IsAny<ProductionPlan>()))
            .Returns(Task.CompletedTask);

        _mockProductionStageRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionStage>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanTaskRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionPlanTask>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = new Group
        {
            Id = groupId,
            GroupName = "Group D",
            TotalArea = 3000m,
            Status = GroupStatus.Active
        };

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Test Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync(group);

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null, null))
            .ReturnsAsync(new List<MaterialPrice>());

        _mockProductionPlanRepo.Setup(r => r.AddAsync(It.IsAny<ProductionPlan>()))
            .Returns(Task.CompletedTask);

        _mockProductionStageRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionStage>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanTaskRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionPlanTask>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }

    [Fact]
    public async Task Handle_InactiveGroup_StillCreates()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = new Group
        {
            Id = groupId,
            GroupName = "Inactive Group",
            TotalArea = 2000m,
            Status = GroupStatus.Draft
        };

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Plan for Inactive Group",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync(group);

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null, null))
            .ReturnsAsync(new List<MaterialPrice>());

        _mockProductionPlanRepo.Setup(r => r.AddAsync(It.IsAny<ProductionPlan>()))
            .Returns(Task.CompletedTask);

        _mockProductionStageRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionStage>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanTaskRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionPlanTask>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Assuming inactive groups can still have plans
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LargeTotalArea_HandlesCorrectly()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = new Group
        {
            Id = groupId,
            GroupName = "Large Group",
            TotalArea = 1000000m,
            Status = GroupStatus.Active
        };

        var command = new CreateProductionPlanCommand
        {
            GroupId = groupId,
            PlanName = "Large Scale Plan",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            Stages = CreateSampleStages()
        };

        _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync(group);

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null, null))
            .ReturnsAsync(new List<MaterialPrice>());

        _mockProductionPlanRepo.Setup(r => r.AddAsync(It.IsAny<ProductionPlan>()))
            .Returns(Task.CompletedTask);

        _mockProductionStageRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionStage>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanTaskRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionPlanTask>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PlanWithStandardPlanId_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateProductionPlanCommand
        {
            StandardPlanId = Guid.NewGuid(),
            PlanName = "Plan from Standard",
            BasePlantingDate = DateTime.UtcNow.AddDays(30),
            TotalArea = 4000m,
            Stages = CreateSampleStages()
        };

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(),
            null, null))
            .ReturnsAsync(new List<MaterialPrice>());

        _mockProductionPlanRepo.Setup(r => r.AddAsync(It.IsAny<ProductionPlan>()))
            .Returns(Task.CompletedTask);

        _mockProductionStageRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionStage>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanTaskRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<ProductionPlanTask>>()))
            .Returns(Task.CompletedTask);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
