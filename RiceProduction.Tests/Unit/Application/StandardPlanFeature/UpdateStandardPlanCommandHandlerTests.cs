using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.StandardPlanFeature.Commands.UpdateStandardPlan;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
using Xunit;
using MediatR;

namespace RiceProduction.Tests.Unit.Application.StandardPlanFeature;

/// <summary>
/// Tests for UpdateStandardPlanCommandHandler - validates standard plan updates
/// </summary>
public class UpdateStandardPlanCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<StandardPlan>> _mockStandardPlanRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<UpdateStandardPlanCommandHandler>> _mockLogger;
    private readonly UpdateStandardPlanCommandHandler _handler;

    public UpdateStandardPlanCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockStandardPlanRepo = new Mock<IGenericRepository<StandardPlan>>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<UpdateStandardPlanCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<StandardPlan>()).Returns(_mockStandardPlanRepo.Object);

        _handler = new UpdateStandardPlanCommandHandler(
            _mockUnitOfWork.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidUpdate_UpdatesSuccessfully()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Old Plan Name",
            Description = "Old Description",
            TotalDurationDays = 90,
            IsActive = true
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Updated Plan Name",
            Description = "Updated Description",
            TotalDurationDays = 100,
            IsActive = true
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.Update(It.IsAny<StandardPlan>()))
            .Verifiable();

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(planId);
        existingPlan.PlanName.Should().Be("Updated Plan Name");
        existingPlan.Description.Should().Be("Updated Description");
        existingPlan.TotalDurationDays.Should().Be(100);
        _mockStandardPlanRepo.Verify(r => r.Update(It.IsAny<StandardPlan>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PlanNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = Guid.NewGuid(),
            PlanName = "Test",
            TotalDurationDays = 90
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync((StandardPlan?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_UpdatePlanName_OnlyUpdatesName()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Original Name",
            Description = "Original Description",
            TotalDurationDays = 90,
            IsActive = true
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "New Name Only",
            Description = "Original Description",
            TotalDurationDays = 90,
            IsActive = true
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        existingPlan.PlanName.Should().Be("New Name Only");
        existingPlan.Description.Should().Be("Original Description");
    }

    [Fact]
    public async Task Handle_ChangeDuration_UpdatesDurationCorrectly()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            IsActive = true
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Test Plan",
            TotalDurationDays = 120,
            IsActive = true
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        existingPlan.TotalDurationDays.Should().Be(120);
    }

    [Fact]
    public async Task Handle_DeactivatePlan_SetsIsActiveToFalse()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Active Plan",
            TotalDurationDays = 90,
            IsActive = true
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Active Plan",
            TotalDurationDays = 90,
            IsActive = false
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        existingPlan.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ActivatePlan_SetsIsActiveToTrue()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Inactive Plan",
            TotalDurationDays = 90,
            IsActive = false
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Inactive Plan",
            TotalDurationDays = 90,
            IsActive = true
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        existingPlan.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UpdateWithLongDescription_StoresFullDescription()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            Description = "Short",
            TotalDurationDays = 90,
            IsActive = true
        };

        var longDescription = string.Join(" ", Enumerable.Repeat("Detailed plan description with comprehensive information.", 20));

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Test Plan",
            Description = longDescription,
            TotalDurationDays = 90,
            IsActive = true
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        existingPlan.Description.Should().Be(longDescription);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            IsActive = true
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Updated Plan",
            TotalDurationDays = 90,
            IsActive = true
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StatusChange_PublishesStatusChangedEvent()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            IsActive = true
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            IsActive = false // Status change
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockMediator.Verify(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoStatusChange_PublishesUpdatedEvent()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var existingPlan = new StandardPlan
        {
            Id = planId,
            PlanName = "Test Plan",
            TotalDurationDays = 90,
            IsActive = true
        };

        var command = new UpdateStandardPlanCommand
        {
            StandardPlanId = planId,
            PlanName = "Updated Plan",
            TotalDurationDays = 100,
            IsActive = true // No status change
        };

        _mockStandardPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<StandardPlan, bool>>>()))
            .ReturnsAsync(existingPlan);

        _mockStandardPlanRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockMediator.Verify(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

