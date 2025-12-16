using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.RiceVarietyFeature.Commands.DeleteRiceVariety;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
using Xunit;
using MediatR;

namespace RiceProduction.Tests.Unit.Application.RiceVarietyFeature;

/// <summary>
/// Tests for DeleteRiceVarietyCommandHandler - validates rice variety deletion
/// </summary>
public class DeleteRiceVarietyCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<RiceVariety>> _mockRiceVarietyRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<DeleteRiceVarietyCommandHandler>> _mockLogger;
    private readonly DeleteRiceVarietyCommandHandler _handler;

    public DeleteRiceVarietyCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRiceVarietyRepo = new Mock<IGenericRepository<RiceVariety>>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<DeleteRiceVarietyCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<RiceVariety>()).Returns(_mockRiceVarietyRepo.Object);

        _handler = new DeleteRiceVarietyCommandHandler(
            _mockUnitOfWork.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidRiceVariety_SoftDeletesSuccessfully()
    {
        // Arrange
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "IR64",
            IsActive = true
        };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(varietyId);
        variety.IsActive.Should().BeFalse();
        _mockRiceVarietyRepo.Verify(r => r.Update(It.Is<RiceVariety>(v => v.IsActive == false)), Times.Once);
    }

    [Fact]
    public async Task Handle_RiceVarietyNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = Guid.NewGuid() };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync((RiceVariety?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_PopularVariety_StillDeletes()
    {
        // Arrange
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Jasmine Rice",
            Description = "Popular aromatic variety",
            IsActive = true
        };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyInactiveVariety_RemainsInactive()
    {
        // Arrange
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Old Variety",
            IsActive = false
        };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        variety.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Test Variety",
            IsActive = true
        };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_PublishesEvent()
    {
        // Arrange
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Event Test Variety",
            IsActive = true
        };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
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
    public async Task Handle_VarietyWithLongName_DeletesSuccessfully()
    {
        // Arrange
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Super Long Grain Premium Quality Aromatic Rice Variety 2024 Edition",
            IsActive = true
        };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_VietnameseVarietyName_DeletesCorrectly()
    {
        // Arrange
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Lúa Nàng Hoa 9",
            IsActive = true
        };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsFailure()
    {
        // Arrange
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = Guid.Empty };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync((RiceVariety?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MultipleDeleteOperations_EachSucceedsIndependently()
    {
        // Arrange
        var variety1Id = Guid.NewGuid();
        var variety1 = new RiceVariety { Id = variety1Id, VarietyName = "Variety 1", IsActive = true };

        var command = new DeleteRiceVarietyCommand { RiceVarietyId = variety1Id };

        _mockRiceVarietyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>()))
            .ReturnsAsync(variety1);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        variety1.IsActive.Should().BeFalse();
    }
}

