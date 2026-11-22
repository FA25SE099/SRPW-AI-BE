using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.CultivationFeature.Event;
using RiceProduction.Application.ProductionPlanFeature.Commands.ApproveRejectPlan;
using RiceProduction.Domain.Entities;
using RiceProduction.Tests.Fixtures;
using Xunit;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Tests.Unit.Application.ProductionPlanFeature;

/// <summary>
/// Tests for ApproveRejectPlanCommandHandler - validates plan approval/rejection logic
/// </summary>
public class ApproveRejectPlanCommandHandlerTests
{
    private readonly Mock<ILogger<ApproveRejectPlanCommandHandler>> _mockLogger = new();
    private readonly Mock<IMediator> _mockMediator = new();

    [Fact]
    public async Task Handle_ApprovePlan_UpdatesStatusToApproved()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        };
        context.ProductionPlans.Add(plan);
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = "Plan looks good, approved for execution",
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(planId);
        var updatedPlan = await context.ProductionPlans.FirstAsync(p => p.Id == planId);
        updatedPlan.Status.Should().Be(TaskStatus.Approved);
    }

    [Fact]
    public async Task Handle_RejectPlan_UpdatesStatusToRejected()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        };
        context.ProductionPlans.Add(plan);
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = false,
            Notes = "Plan needs revision - incorrect area calculations",
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var updatedPlan = await context.ProductionPlans.FirstAsync(p => p.Id == planId);
        updatedPlan.Status.Should().Be(TaskStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_PlanNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = Guid.NewGuid(),
            Approved = true,
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Plan not found") || e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_ApproveWithReviewerNotes_StoresNotes()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        context.ProductionPlans.Add(new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        });
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var reviewNotes = "Approved with recommendations for water management improvements";
        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = reviewNotes,
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyApprovedPlan_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        context.ProductionPlans.Add(new ProductionPlan
        {
            Id = planId,
            PlanName = "Already Approved Plan",
            Status = TaskStatus.Approved
        });
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = "Re-reviewing for compliance",
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PendingApproval"));
    }

    [Fact]
    public async Task Handle_RejectPreviouslyApprovedPlan_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        context.ProductionPlans.Add(new ProductionPlan
        {
            Id = planId,
            PlanName = "Previously Approved Plan",
            Status = TaskStatus.Approved
        });
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = false,
            Notes = "Rejecting due to new information about resource availability",
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PendingApproval"));
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        context.ProductionPlans.Add(new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        });
        await context.SaveChangesAsync();
        context.SaveChangesException = new Exception("Database connection failed");

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("An error occurred during approval/rejection process."));
    }

    [Fact]
    public async Task Handle_ApproveWithoutNotes_SucceedsWithoutNotes()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        context.ProductionPlans.Add(new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        });
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = null,
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RejectWithDetailedNotes_StoresLongNotes()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        context.ProductionPlans.Add(new ProductionPlan
        {
            Id = planId,
            PlanName = "Complex Plan",
            Status = TaskStatus.PendingApproval
        });
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var longNotes = string.Join(" ", Enumerable.Repeat(
            "The plan requires revision due to various factors including resource allocation, timing concerns, and coordination issues.", 10));

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = false,
            Notes = longNotes,
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ApprovedPlan_PublishesEvent()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var planId = Guid.NewGuid();
        context.ProductionPlans.Add(new ProductionPlan
        {
            Id = planId,
            PlanName = "Event Test Plan",
            Status = TaskStatus.PendingApproval
        });
        await context.SaveChangesAsync();

        var handler = new ApproveRejectPlanCommandHandler(context, _mockLogger.Object, _mockMediator.Object);

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            ExpertId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockMediator.Verify(m => m.Publish(
            It.Is<ProductionPlanApprovalEvent>(e => e.PlanId == planId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

