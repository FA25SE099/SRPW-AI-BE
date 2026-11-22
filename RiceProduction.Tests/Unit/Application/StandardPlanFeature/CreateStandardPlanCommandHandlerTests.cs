using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.StandardPlanFeature.Commands.CreateStandardPlan;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.StandardPlanFeature;

/// <summary>
/// Tests for CreateStandardPlanCommandHandler - validates standard plan creation
/// </summary>
public class CreateStandardPlanCommandHandlerTests : IDisposable
{
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IUser> _mockUser;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreateStandardPlanCommandHandler>> _mockLogger;
    private readonly CreateStandardPlanCommandHandler _handler;
    private readonly Guid _expertId;

    public CreateStandardPlanCommandHandlerTests()
    {
        _context = TestApplicationDbContext.Create();
        _mockUser = new Mock<IUser>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreateStandardPlanCommandHandler>>();

        // Setup default authenticated user (Agronomy Expert)
        _expertId = Guid.NewGuid();
        _mockUser.Setup(u => u.Id).Returns(_expertId);
        _mockUser.Setup(u => u.Roles).Returns(new List<string> { "AgronomyExpert" });

        _context.AgronomyExperts.Add(new AgronomyExpert
        {
            Id = _expertId,
            FullName = "Test Expert",
            UserName = "expert@test.com",
            Email = "expert@test.com"
        });
        _context.SaveChanges();

        _handler = new CreateStandardPlanCommandHandler(
            _context,
            _mockUser.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private void SeedCategory(Guid categoryId, string name = "Short Duration")
    {
        _context.RiceVarietyCategories.Add(new RiceVarietyCategory
        {
            Id = categoryId,
            CategoryName = name,
            MinGrowthDays = 80,
            MaxGrowthDays = 100
        });
        _context.SaveChanges();
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
        SeedCategory(categoryId);

        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Short Duration Rice Standard Plan",
            Description = "Standard cultivation plan for short duration rice varieties",
            TotalDurationDays = 90,
            IsActive = true,
            Stages = CreateSampleStages()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        var saved = await _context.StandardPlans
            .Include(p => p.StandardPlanStages)
            .FirstOrDefaultAsync(p => p.Id == result.Data);
        saved.Should().NotBeNull();
        saved!.PlanName.Should().Be("Short Duration Rice Standard Plan");
        saved.TotalDurationDays.Should().Be(90);
        saved.CategoryId.Should().Be(categoryId);
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

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Category") && e.Contains("not found"));
        (await _context.StandardPlans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_DuplicatePlanName_ReturnsFailure()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        SeedCategory(categoryId);

        _context.StandardPlans.Add(new StandardPlan
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            ExpertId = _expertId,
            PlanName = "Existing Plan",
            TotalDurationDays = 90
        });
        await _context.SaveChangesAsync();

        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Existing Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
        (await _context.StandardPlans.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ReturnsFailure()
    {
        // Arrange
        var unauthenticatedUser = new Mock<IUser>();
        unauthenticatedUser.Setup(u => u.Id).Returns((Guid?)null);

        var handler = new CreateStandardPlanCommandHandler(
            _context,
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

        var categoryId = Guid.NewGuid();
        SeedCategory(categoryId);

        var handler = new CreateStandardPlanCommandHandler(
            _context,
            nonExpertUser.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

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
        SeedCategory(categoryId);

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

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = await _context.StandardPlans
            .Include(p => p.StandardPlanStages)
            .FirstOrDefaultAsync(p => p.Id == result.Data);
        saved!.StandardPlanStages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_InactivePlan_CreatesWithInactiveStatus()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        SeedCategory(categoryId);

        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Inactive Plan",
            TotalDurationDays = 90,
            IsActive = false,
            Stages = CreateSampleStages()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = await _context.StandardPlans.FirstOrDefaultAsync(p => p.Id == result.Data);
        saved.Should().NotBeNull();
        saved!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        SeedCategory(categoryId);

        var command = new CreateStandardPlanCommand
        {
            CategoryId = categoryId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            Stages = CreateSampleStages()
        };

        _context.SaveChangesException = new Exception("Database error");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }
}
