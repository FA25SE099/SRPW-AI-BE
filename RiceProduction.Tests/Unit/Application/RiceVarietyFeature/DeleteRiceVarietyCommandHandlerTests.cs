using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.RiceVarietyFeature.Commands.DeleteRiceVariety;
using RiceProduction.Domain.Entities;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.RiceVarietyFeature;

/// <summary>
/// Tests for DeleteRiceVarietyCommandHandler - validates rice variety deletion
/// </summary>
public class DeleteRiceVarietyCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator = new();
    private readonly Mock<ILogger<DeleteRiceVarietyCommandHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ValidRiceVariety_SoftDeletesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "IR64",
            IsActive = true
        };

        context.RiceVarieties.Add(variety);
        await context.SaveChangesAsync();

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(varietyId);
        var updated = await context.RiceVarieties.AsNoTracking().FirstAsync(v => v.Id == varietyId);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RiceVarietyNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = Guid.NewGuid() };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_PopularVariety_StillDeletes()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Jasmine Rice",
            Description = "Popular aromatic variety",
            IsActive = true
        };

        context.RiceVarieties.Add(variety);
        await context.SaveChangesAsync();

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyInactiveVariety_RemainsInactive()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Old Variety",
            IsActive = false
        };

        context.RiceVarieties.Add(variety);
        await context.SaveChangesAsync();

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        variety.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Test Variety",
            IsActive = true
        };

        context.RiceVarieties.Add(variety);
        await context.SaveChangesAsync();
        context.SaveChangesException = new Exception("Database connection failed");

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_PublishesEvent()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Event Test Variety",
            IsActive = true
        };

        context.RiceVarieties.Add(variety);
        await context.SaveChangesAsync();

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

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
        await using var context = TestApplicationDbContext.Create();
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Super Long Grain Premium Quality Aromatic Rice Variety 2024 Edition",
            IsActive = true
        };

        context.RiceVarieties.Add(variety);
        await context.SaveChangesAsync();

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_VietnameseVarietyName_DeletesCorrectly()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var varietyId = Guid.NewGuid();
        var variety = new RiceVariety
        {
            Id = varietyId,
            VarietyName = "Lúa Nàng Hoa 9",
            IsActive = true
        };

        context.RiceVarieties.Add(variety);
        await context.SaveChangesAsync();

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = varietyId };

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
        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = Guid.Empty };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MultipleDeleteOperations_EachSucceedsIndependently()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var variety1Id = Guid.NewGuid();
        var variety1 = new RiceVariety { Id = variety1Id, VarietyName = "Variety 1", IsActive = true };

        context.RiceVarieties.Add(variety1);
        await context.SaveChangesAsync();

        var handler = new DeleteRiceVarietyCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var command = new DeleteRiceVarietyCommand { RiceVarietyId = variety1Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        variety1.IsActive.Should().BeFalse();
    }
}
