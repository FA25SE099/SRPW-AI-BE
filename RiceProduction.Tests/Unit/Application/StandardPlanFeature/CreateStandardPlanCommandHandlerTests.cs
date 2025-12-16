using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.StandardPlanFeature.Commands.CreateStandardPlan;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;
using Xunit;
using MediatR;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Tests.Unit.Application.StandardPlanFeature;

/// <summary>
/// Tests for CreateStandardPlanCommandHandler - validates standard plan creation
/// </summary>
public class CreateStandardPlanCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<StandardPlan>> _mockStandardPlanRepo;
    private readonly Mock<IGenericRepository<RiceVarietyCategory>> _mockCategoryRepo;
    private readonly Mock<IGenericRepository<Material>> _mockMaterialRepo;
    private readonly Mock<IAgronomyExpertRepository> _mockAgronomyExpertRepo;
    private readonly Mock<IUser> _mockUser;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreateStandardPlanCommandHandler>> _mockLogger;
    private readonly CreateStandardPlanCommandHandler _handler;
    private readonly Guid _expertId;

    public CreateStandardPlanCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockStandardPlanRepo = new Mock<IGenericRepository<StandardPlan>>();
        _mockCategoryRepo = new Mock<IGenericRepository<RiceVarietyCategory>>();
        _mockMaterialRepo = new Mock<IGenericRepository<Material>>();
        _mockAgronomyExpertRepo = new Mock<IAgronomyExpertRepository>();
        _mockUser = new Mock<IUser>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreateStandardPlanCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<StandardPlan>()).Returns(_mockStandardPlanRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<RiceVarietyCategory>()).Returns(_mockCategoryRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Material>()).Returns(_mockMaterialRepo.Object);
        _mockUnitOfWork.Setup(u => u.AgronomyExpertRepository).Returns(_mockAgronomyExpertRepo.Object);

        // Setup default authenticated user (Agronomy Expert)
        _expertId = Guid.NewGuid();
        _mockUser.Setup(u => u.Id).Returns(_expertId);
        _mockUser.Setup(u => u.Roles).Returns(new List<string> { "AgronomyExpert" });

        // Setup default expert exists
        _mockAgronomyExpertRepo.Setup(r => r.ExistAsync(_expertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new CreateStandardPlanCommandHandler(
            _mockUnitOfWork.Object,
            _mockUser.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    private List<StandardPlanStageRequest> CreateSampleStages()
    {
        return new List<StandardPlanStageRequest>
        {
            new StandardPlanStageRequest
            {
                StageName = "Land Preparation",
                SequenceOrder = 1,
                ExpectedDurationDays = 7,
                IsMandatory = true,
                Tasks = new List<StandardPlanTaskRequest>
                {
                    new StandardPlanTaskRequest
                    {
                        TaskName = "Plowing",
                        TaskType = TaskType.LandPreparation,
                        DaysAfter = 0,
                        DurationDays = 2,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1,
                        Materials = new List<StandardPlanTaskMaterialRequest>()
                    }
                }
            }
        };
    }

    [Fact]
    public async Task Handle_ValidStandardPlan_CreatesSuccessfully()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Short Duration Rice Standard Plan",
            Description = "Standard cultivation plan for short duration rice varieties",
            TotalDurationDays = 90,
            IsActive = true,
            Stages = CreateSampleStages()
        };

        _mockCategoryRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<RiceVarietyCategory, bool>>>()))
            .ReturnsAsync(true);

        _mockStandardPlanRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(false); // No duplicate

        _mockMaterialRepo.Setup(r => r.GetQueryable())
            .Returns(new List<Material>().AsQueryable());

        _mockStandardPlanRepo.Setup(r => r.AddAsync(It.IsAny<StandardPlan>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        _mockStandardPlanRepo.Verify(r => r.AddAsync(It.Is<StandardPlan>(
            p => p.PlanName == "Short Duration Rice Standard Plan" &&
                 p.TotalDurationDays == 90 &&
                 p.CategoryId == categoryId)), Times.Once);
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new CreateStandardPlanCommand
        {
            CategoryId = Guid.NewGuid(),
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

        _mockCategoryRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<RiceVarietyCategory, bool>>>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Category") && e.Contains("not found"));
        _mockStandardPlanRepo.Verify(r => r.AddAsync(It.IsAny<StandardPlan>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicatePlanName_ReturnsFailure()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Existing Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

        _mockCategoryRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<RiceVarietyCategory, bool>>>()))
            .ReturnsAsync(true);

        _mockStandardPlanRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(true); // Duplicate exists

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
        _mockStandardPlanRepo.Verify(r => r.AddAsync(It.IsAny<StandardPlan>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ReturnsFailure()
    {
        // Arrange
        var unauthenticatedUser = new Mock<IUser>();
        unauthenticatedUser.Setup(u => u.Id).Returns((Guid?)null);

        var handler = new CreateStandardPlanCommandHandler(
            _mockUnitOfWork.Object,
            unauthenticatedUser.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var command = new CreateStandardPlanCommand
        {
            CategoryId = Guid.NewGuid(),
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not authenticated") || e.Contains("Unauthorized"));
    }

    [Fact]
    public async Task Handle_UserNotAnExpert_ReturnsFailure()
    {
        // Arrange
        var nonExpertId = Guid.NewGuid();
        var nonExpertUser = new Mock<IUser>();
        nonExpertUser.Setup(u => u.Id).Returns(nonExpertId);
        nonExpertUser.Setup(u => u.Roles).Returns(new List<string> { "Farmer" });

        _mockAgronomyExpertRepo.Setup(r => r.ExistAsync(nonExpertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Not an expert

        var handler = new CreateStandardPlanCommandHandler(
            _mockUnitOfWork.Object,
            nonExpertUser.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var command = new CreateStandardPlanCommand
        {
            CategoryId = Guid.NewGuid(),
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

        _mockCategoryRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<RiceVarietyCategory, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Expert") || e.Contains("NotAnExpert"));
    }

    [Fact]
    public async Task Handle_PlanWithMultipleStages_CreatesSuccessfully()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var stages = new List<StandardPlanStageRequest>
        {
            new StandardPlanStageRequest
            {
                StageName = "Land Preparation",
                SequenceOrder = 1,
                ExpectedDurationDays = 7,
                Tasks = new List<StandardPlanTaskRequest>
                {
                    new StandardPlanTaskRequest
                    {
                        TaskName = "Plowing",
                        TaskType = TaskType.LandPreparation,
                        DaysAfter = 0,
                        DurationDays = 2,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1,
                        Materials = new List<StandardPlanTaskMaterialRequest>()
                    }
                }
            },
            new StandardPlanStageRequest
            {
                StageName = "Planting",
                SequenceOrder = 2,
                ExpectedDurationDays = 30,
                Tasks = new List<StandardPlanTaskRequest>
                {
                    new StandardPlanTaskRequest
                    {
                        TaskName = "Seed Sowing",
                        TaskType = TaskType.Sowing,
                        DaysAfter = 7,
                        DurationDays = 1,
                        Priority = TaskPriority.Critical,
                        SequenceOrder = 1,
                        Materials = new List<StandardPlanTaskMaterialRequest>()
                    }
                }
            }
        };

        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Complete Standard Plan",
            TotalDurationDays = 90,
            Stages = stages
        };

        _mockCategoryRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<RiceVarietyCategory, bool>>>()))
            .ReturnsAsync(true);

        _mockStandardPlanRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(false);

        _mockMaterialRepo.Setup(r => r.GetQueryable())
            .Returns(new List<Material>().AsQueryable());

        _mockStandardPlanRepo.Setup(r => r.AddAsync(It.IsAny<StandardPlan>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InactivePlan_CreatesWithInactiveStatus()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Inactive Plan",
            TotalDurationDays = 90,
            IsActive = false,
            Stages = CreateSampleStages()
        };

        _mockCategoryRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<RiceVarietyCategory, bool>>>()))
            .ReturnsAsync(true);

        _mockStandardPlanRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(false);

        _mockMaterialRepo.Setup(r => r.GetQueryable())
            .Returns(new List<Material>().AsQueryable());

        _mockStandardPlanRepo.Setup(r => r.AddAsync(It.IsAny<StandardPlan>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockStandardPlanRepo.Verify(r => r.AddAsync(It.Is<StandardPlan>(
            p => p.IsActive == false)), Times.Once);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

        _mockCategoryRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<RiceVarietyCategory, bool>>>()))
            .ReturnsAsync(true);

        _mockStandardPlanRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(false);

        _mockMaterialRepo.Setup(r => r.GetQueryable())
            .Returns(new List<Material>().AsQueryable());

        _mockStandardPlanRepo.Setup(r => r.AddAsync(It.IsAny<StandardPlan>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }
}
