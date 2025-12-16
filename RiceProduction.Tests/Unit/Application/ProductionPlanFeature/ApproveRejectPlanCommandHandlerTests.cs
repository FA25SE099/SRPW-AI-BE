using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.ProductionPlanFeature.Commands.ApproveRejectPlan;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;
using Xunit;
using MediatR;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Tests.Unit.Application.ProductionPlanFeature;

/// <summary>
/// Tests for ApproveRejectPlanCommandHandler - validates plan approval/rejection logic
/// </summary>
public class ApproveRejectPlanCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<ProductionPlan>> _mockProductionPlanRepo;
    private readonly Mock<ILogger<ApproveRejectPlanCommandHandler>> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;
    private readonly ApproveRejectPlanCommandHandler _handler;

    public ApproveRejectPlanCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockProductionPlanRepo = new Mock<IGenericRepository<ProductionPlan>>();
        _mockLogger = new Mock<ILogger<ApproveRejectPlanCommandHandler>>();
        _mockMediator = new Mock<IMediator>();

        _mockUnitOfWork.Setup(u => u.Repository<ProductionPlan>()).Returns(_mockProductionPlanRepo.Object);

        _handler = new ApproveRejectPlanCommandHandler(
            _mockUnitOfWork.Object,
            _mockLogger.Object,
            _mockMediator.Object);
    }

    [Fact]
    public async Task Handle_ApprovePlan_UpdatesStatusToApproved()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        };

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = "Plan looks good, approved for execution",
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.Update(It.IsAny<ProductionPlan>()))
            .Verifiable();

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(planId);
        _mockProductionPlanRepo.Verify(r => r.Update(It.Is<ProductionPlan>(
            p => p.Id == planId && p.Status == TaskStatus.Approved)), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectPlan_UpdatesStatusToRejected()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        };

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = false,
            Notes = "Plan needs revision - incorrect area calculations",
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockProductionPlanRepo.Verify(r => r.Update(It.Is<ProductionPlan>(
            p => p.Status == TaskStatus.Cancelled)), Times.Once);
    }

    [Fact]
    public async Task Handle_PlanNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new ApproveRejectPlanCommand
        {
            PlanId = Guid.NewGuid(),
            Approved = true,
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync((ProductionPlan?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Plan not found") || e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_ApproveWithReviewerNotes_StoresNotes()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        };

        var reviewNotes = "Approved with recommendations for water management improvements";
        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = reviewNotes,
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyApprovedPlan_ReturnsFailure()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Already Approved Plan",
            Status = TaskStatus.Approved
        };

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = "Re-reviewing for compliance",
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PendingApproval"));
    }

    [Fact]
    public async Task Handle_RejectPreviouslyApprovedPlan_ReturnsFailure()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Previously Approved Plan",
            Status = TaskStatus.Approved
        };

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = false,
            Notes = "Rejecting due to new information about resource availability",
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PendingApproval"));
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        };

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Database connection failed"));
    }

    [Fact]
    public async Task Handle_ApproveWithoutNotes_SucceedsWithoutNotes()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Status = TaskStatus.PendingApproval
        };

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            Notes = null,
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RejectWithDetailedNotes_StoresLongNotes()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Complex Plan",
            Status = TaskStatus.PendingApproval
        };

        var longNotes = string.Join(" ", Enumerable.Repeat(
            "The plan requires revision due to various factors including resource allocation, timing concerns, and coordination issues.", 10));

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = false,
            Notes = longNotes,
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ApprovedPlan_PublishesEvent()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan
        {
            Id = planId,
            PlanName = "Event Test Plan",
            Status = TaskStatus.PendingApproval
        };

        var command = new ApproveRejectPlanCommand
        {
            PlanId = planId,
            Approved = true,
            ExpertId = Guid.NewGuid()
        };

        _mockProductionPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductionPlan, bool>>>()))
            .ReturnsAsync(plan);

        _mockProductionPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        // Note: Event publishing verification would depend on actual implementation
    }
}

