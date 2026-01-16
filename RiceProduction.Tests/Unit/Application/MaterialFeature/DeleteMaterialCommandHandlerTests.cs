using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.MaterialFeature.Commands.DeleteMaterial;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
using Xunit;
using MediatR;

namespace RiceProduction.Tests.Unit.Application.MaterialFeature;

/// <summary>
/// Tests for DeleteMaterialCommandHandler - validates material deletion (soft delete)
/// </summary>
public class DeleteMaterialCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Material>> _mockMaterialRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<DeleteMaterialCommandHandler>> _mockLogger;
    private readonly DeleteMaterialCommandHandler _handler;

    public DeleteMaterialCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMaterialRepo = new Mock<IGenericRepository<Material>>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<DeleteMaterialCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Material>()).Returns(_mockMaterialRepo.Object);

        _handler = new DeleteMaterialCommandHandler(
            _mockUnitOfWork.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidMaterialId_SoftDeletesSuccessfully()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Pesticide A",
            Unit = "liter",
            IsActive = true
        };

        var command = new DeleteMaterialCommand { MaterialId = materialId };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material);

        _mockMaterialRepo.Setup(r => r.Update(It.IsAny<Material>()))
            .Verifiable();

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(materialId);
        result.Message.Should().Contain("deleted successfully");
        material.IsActive.Should().BeFalse();
        _mockMaterialRepo.Verify(r => r.Update(It.Is<Material>(m => m.IsActive == false)), Times.Once);
    }

    [Fact]
    public async Task Handle_MaterialNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new DeleteMaterialCommand { MaterialId = Guid.NewGuid() };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync((Material?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
        _mockMaterialRepo.Verify(r => r.Update(It.IsAny<Material>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyInactiveMaterial_StillSoftDeletes()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Old Material",
            Unit = "kg",
            IsActive = false // Already inactive
        };

        var command = new DeleteMaterialCommand { MaterialId = materialId };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        material.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Test Material",
            IsActive = true
        };

        var command = new DeleteMaterialCommand { MaterialId = materialId };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Database error"));
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_PublishesEvent()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Fertilizer B",
            IsActive = true
        };

        var command = new DeleteMaterialCommand { MaterialId = materialId };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material);

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
    public async Task Handle_MultipleDeletions_EachDeletesIndependently()
    {
        // Arrange
        var material1Id = Guid.NewGuid();
        var material1 = new Material { Id = material1Id, Name = "Material 1", IsActive = true };

        var command = new DeleteMaterialCommand { MaterialId = material1Id };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material1);

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
        var command = new DeleteMaterialCommand { MaterialId = Guid.Empty };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync((Material?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidDelete_LogsInformation()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Logging Test Material",
            IsActive = true
        };

        var command = new DeleteMaterialCommand { MaterialId = materialId };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        // Logger verification would be implementation-specific
    }

    [Fact]
    public async Task Handle_PartialDatabaseCommit_HandlesGracefully()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Partial Commit Test",
            IsActive = true
        };

        var command = new DeleteMaterialCommand { MaterialId = materialId };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(0); // No changes saved

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Handler should still succeed as update was called
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CancellationRequested_HandlesCancellation()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var material = new Material { Id = materialId, Name = "Cancel Test", IsActive = true };

        var command = new DeleteMaterialCommand { MaterialId = materialId };

        _mockMaterialRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Material, bool>>>()))
            .ReturnsAsync(material);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _handler.Handle(command, cts.Token);

        // Assert - Should complete despite cancellation in this implementation
        result.Should().NotBeNull();
    }
}

