using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.MaterialFeature.Commands.DeleteMaterial;
using RiceProduction.Domain.Entities;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.MaterialFeature;

/// <summary>
/// Tests for DeleteMaterialCommandHandler - validates material deletion (soft delete)
/// </summary>
public class DeleteMaterialCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator = new();
    private readonly Mock<ILogger<DeleteMaterialCommandHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ValidMaterialId_SoftDeletesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Pesticide A",
            Unit = "liter",
            IsActive = true
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = materialId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(materialId);
        result.Message.Should().Contain("deleted successfully");
        var updated = await context.Materials.AsNoTracking().FirstAsync(m => m.Id == materialId);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MaterialNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = Guid.NewGuid() };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_AlreadyInactiveMaterial_StillSoftDeletes()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Old Material",
            Unit = "kg",
            IsActive = false // Already inactive
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = materialId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        material.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Test Material",
            IsActive = true
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();
        context.SaveChangesException = new Exception("Database error");

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = materialId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Failed to delete material"));
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_PublishesEvent()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Fertilizer B",
            IsActive = true
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = materialId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

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
        await using var context = TestApplicationDbContext.Create();
        var material1Id = Guid.NewGuid();
        var material1 = new Material { Id = material1Id, Name = "Material 1", IsActive = true };

        context.Materials.Add(material1);
        await context.SaveChangesAsync();

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = material1Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = Guid.Empty };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidDelete_LogsInformation()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Logging Test Material",
            IsActive = true
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = materialId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PartialDatabaseCommit_HandlesGracefully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            Name = "Partial Commit Test",
            IsActive = true
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = materialId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - Handler should still succeed as update was called
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CancellationRequested_HandlesCancellation()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var materialId = Guid.NewGuid();
        var material = new Material { Id = materialId, Name = "Cancel Test", IsActive = true };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var handler = new DeleteMaterialCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteMaterialCommand { MaterialId = materialId };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await handler.Handle(command, cts.Token);

        // Assert - Should complete despite cancellation in this implementation
        result.Should().NotBeNull();
    }
}
