using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.PestProtocolFeature.Commands.CreatePestProtocol;
using RiceProduction.Domain.Entities;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.PestProtocolFeature;

/// <summary>
/// Tests for CreatePestProtocolCommandHandler - validates pest protocol creation
/// </summary>
public class CreatePestProtocolCommandHandlerTests
{
    private readonly Mock<ILogger<CreatePestProtocolCommandHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ValidProtocol_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Brown Planthopper Control",
            Type = "Insect",
            Description = "Protocol for controlling brown planthopper infestations",
            ImageLinks = new List<string> { "https://example.com/image1.jpg" },
            IsActive = true,
            Notes = "Apply during early morning or late evening"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        var saved = await context.PestProtocols.AsNoTracking().FirstOrDefaultAsync(p => p.Id == result.Data);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be(command.Name);
        saved.Type.Should().Be(command.Type);
    }

    [Fact]
    public async Task Handle_ProtocolWithoutOptionalFields_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Manual Pest Removal",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateProtocolName_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        context.PestProtocols.Add(new PestProtocol
        {
            Id = Guid.NewGuid(),
            Name = "Existing Protocol",
            Type = "Disease",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Existing Protocol",
            Type = "Disease",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
        (await context.PestProtocols.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_ProtocolWithMultipleImages_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Comprehensive Pest Control",
            Type = "Multiple Pests",
            Description = "Integrated pest management protocol",
            ImageLinks = new List<string>
            {
                "https://example.com/image1.jpg",
                "https://example.com/image2.jpg",
                "https://example.com/image3.jpg"
            },
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = await context.PestProtocols.AsNoTracking().FirstAsync(p => p.Id == result.Data);
        saved.ImageLinks.Should().NotBeNull().And.HaveCount(3);
    }

    [Fact]
    public async Task Handle_LongDescription_StoresFullDescription()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var longDescription = string.Join(" ", Enumerable.Repeat(
            "Apply protocol according to guidelines. Monitor field conditions regularly.", 20));

        var command = new CreatePestProtocolCommand
        {
            Name = "Detailed Protocol",
            Type = "Various Pests",
            Description = longDescription,
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = await context.PestProtocols.AsNoTracking().FirstAsync(p => p.Id == result.Data);
        saved.Description.Should().NotBeNull().And.Contain("Monitor field conditions");
    }

    [Fact]
    public async Task Handle_InactiveProtocol_CreatesWithInactiveStatus()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Archived Protocol",
            Type = "Historical",
            IsActive = false
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = await context.PestProtocols.AsNoTracking().FirstAsync(p => p.Id == result.Data);
        saved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        context.SaveChangesException = new Exception("Database connection failed");
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Test Protocol",
            Type = "Test Pest",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }

    [Fact]
    public async Task Handle_ProtocolWithNotes_StoresNotes()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Standard Protocol",
            Type = "Common Pest",
            Description = "Standard treatment protocol",
            Notes = "Important safety precautions apply",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = await context.PestProtocols.AsNoTracking().FirstAsync(p => p.Id == result.Data);
        saved.Notes.Should().NotBeNull().And.Contain("safety precautions");
    }

    [Fact]
    public async Task Handle_SpecialCharactersInName_HandlesCorrectly()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Giao thức kiểm soát sâu bệnh #1",
            Type = "Sâu đục thân",
            Description = "Phun thuốc trừ sâu",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyImageLinks_FiltersOut()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "Test Protocol",
            Type = "Test Type",
            ImageLinks = new List<string> { "https://example.com/image1.jpg", "", "   ", "https://example.com/image2.jpg" },
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = await context.PestProtocols.AsNoTracking().FirstAsync(p => p.Id == result.Data);
        saved.ImageLinks.Should().NotBeNull().And.HaveCount(2);
    }

    [Fact]
    public async Task Handle_CaseInsensitiveDuplicateCheck_DetectsDuplicate()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        context.PestProtocols.Add(new PestProtocol
        {
            Id = Guid.NewGuid(),
            Name = "brown planthopper",
            Type = "Insect",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var handler = new CreatePestProtocolCommandHandler(context, _mockLogger.Object);

        var command = new CreatePestProtocolCommand
        {
            Name = "BROWN PLANTHOPPER",
            Type = "Insect",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
    }
}
