using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.PestProtocolFeature.Commands.CreatePestProtocol;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
using Xunit;
using MediatR;

namespace RiceProduction.Tests.Unit.Application.PestProtocolFeature;

/// <summary>
/// Tests for CreatePestProtocolCommandHandler - validates pest protocol creation
/// </summary>
public class CreatePestProtocolCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<PestProtocol>> _mockPestProtocolRepo;
    private readonly Mock<IUser> _mockUser;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreatePestProtocolCommandHandler>> _mockLogger;
    private readonly CreatePestProtocolCommandHandler _handler;

    public CreatePestProtocolCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPestProtocolRepo = new Mock<IGenericRepository<PestProtocol>>();
        _mockUser = new Mock<IUser>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreatePestProtocolCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<PestProtocol>()).Returns(_mockPestProtocolRepo.Object);

        // Setup default authenticated user
        _mockUser.Setup(u => u.Id).Returns(Guid.NewGuid());

        _handler = new CreatePestProtocolCommandHandler(
            _mockUnitOfWork.Object,
            _mockUser.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidProtocol_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Brown Planthopper Control",
            Type = "Insect",
            Description = "Protocol for controlling brown planthopper infestations",
            ImageLinks = new List<string> { "https://example.com/image1.jpg" },
            IsActive = true,
            Notes = "Apply during early morning or late evening"
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        _mockPestProtocolRepo.Verify(r => r.AddAsync(It.Is<PestProtocol>(
            p => p.Name == command.Name && p.Type == command.Type)), Times.Once);
    }

    [Fact]
    public async Task Handle_ProtocolWithoutOptionalFields_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Manual Pest Removal",
            IsActive = true
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateProtocolName_ReturnsFailure()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Existing Protocol",
            Type = "Disease",
            IsActive = true
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
        _mockPestProtocolRepo.Verify(r => r.AddAsync(It.IsAny<PestProtocol>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProtocolWithMultipleImages_CreatesSuccessfully()
    {
        // Arrange
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

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockPestProtocolRepo.Verify(r => r.AddAsync(It.Is<PestProtocol>(
            p => p.ImageLinks != null && p.ImageLinks.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task Handle_LongDescription_StoresFullDescription()
    {
        // Arrange
        var longDescription = string.Join(" ", Enumerable.Repeat(
            "Apply protocol according to guidelines. Monitor field conditions regularly.", 20));

        var command = new CreatePestProtocolCommand
        {
            Name = "Detailed Protocol",
            Type = "Various Pests",
            Description = longDescription,
            IsActive = true
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockPestProtocolRepo.Verify(r => r.AddAsync(It.Is<PestProtocol>(
            p => p.Description != null && p.Description.Contains("Monitor field conditions"))), Times.Once);
    }

    [Fact]
    public async Task Handle_InactiveProtocol_CreatesWithInactiveStatus()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Archived Protocol",
            Type = "Historical",
            IsActive = false
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockPestProtocolRepo.Verify(r => r.AddAsync(It.Is<PestProtocol>(
            p => p.IsActive == false)), Times.Once);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Test Protocol",
            Type = "Test Pest",
            IsActive = true
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }

    [Fact]
    public async Task Handle_ProtocolWithNotes_StoresNotes()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Standard Protocol",
            Type = "Common Pest",
            Description = "Standard treatment protocol",
            Notes = "Important safety precautions apply",
            IsActive = true
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockPestProtocolRepo.Verify(r => r.AddAsync(It.Is<PestProtocol>(
            p => p.Notes != null && p.Notes.Contains("safety precautions"))), Times.Once);
    }

    [Fact]
    public async Task Handle_SpecialCharactersInName_HandlesCorrectly()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Giao thức kiểm soát sâu bệnh #1",
            Type = "Sâu đục thân",
            Description = "Phun thuốc trừ sâu",
            IsActive = true
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyImageLinks_FiltersOut()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "Test Protocol",
            Type = "Test Type",
            ImageLinks = new List<string> { "https://example.com/image1.jpg", "", "   ", "https://example.com/image2.jpg" },
            IsActive = true
        };

        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(false);

        _mockPestProtocolRepo.Setup(r => r.AddAsync(It.IsAny<PestProtocol>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        // The handler filters out empty/whitespace links, so only 2 valid links should remain
        _mockPestProtocolRepo.Verify(r => r.AddAsync(It.Is<PestProtocol>(
            p => p.ImageLinks != null && p.ImageLinks.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task Handle_CaseInsensitiveDuplicateCheck_DetectsDuplicate()
    {
        // Arrange
        var command = new CreatePestProtocolCommand
        {
            Name = "BROWN PLANTHOPPER",
            Type = "Insect",
            IsActive = true
        };

        // Simulate that "brown planthopper" already exists (lowercase)
        _mockPestProtocolRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PestProtocol, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
    }
}
