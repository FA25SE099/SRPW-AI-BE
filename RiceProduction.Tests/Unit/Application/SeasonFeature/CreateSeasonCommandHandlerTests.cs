using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.SeasonFeature.Commands.CreateSeason;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
using Xunit;
using MediatR;

namespace RiceProduction.Tests.Unit.Application.SeasonFeature;

/// <summary>
/// Tests for CreateSeasonCommandHandler - validates season creation
/// </summary>
public class CreateSeasonCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Season>> _mockSeasonRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreateSeasonCommandHandler>> _mockLogger;
    private readonly CreateSeasonCommandHandler _handler;

    public CreateSeasonCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSeasonRepo = new Mock<IGenericRepository<Season>>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreateSeasonCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Season>()).Returns(_mockSeasonRepo.Object);

        _handler = new CreateSeasonCommandHandler(
            _mockUnitOfWork.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidSeason_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Winter-Spring",
            StartDate = "1/1",
            EndDate = "5/31"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockSeasonRepo.Setup(r => r.AddAsync(It.IsAny<Season>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        _mockSeasonRepo.Verify(r => r.AddAsync(It.Is<Season>(
            s => s.SeasonName == "Winter-Spring" && 
                 s.StartDate == "1/1" &&
                 s.EndDate == "5/31")), Times.Once);
    }

    [Fact]
    public async Task Handle_SummerAutumnSeason_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Summer-Autumn",
            StartDate = "6/1",
            EndDate = "9/30",
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AutumnWinterSeason_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Autumn-Winter",
            StartDate = "10/1",
            EndDate = "12/31"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateSeasonName_ReturnsFailure()
    {
        // Arrange
        var existingSeason = new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Winter-Spring",
            StartDate = "1/1",
            EndDate = "5/31"
        };

        var command = new CreateSeasonCommand
        {
            SeasonName = "Winter-Spring",
            StartDate = "1/1",
            EndDate = "5/31"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync(existingSeason);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists") || e.Contains("duplicate"));
    }

    [Fact]
    public async Task Handle_SeasonWithSeasonType_StoresSeasonType()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Detailed Season",
            StartDate = "3/1",
            EndDate = "7/31",
            SeasonType = "Main Crop"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockSeasonRepo.Verify(r => r.AddAsync(It.Is<Season>(
            s => s.SeasonType == "Main Crop")), Times.Once);
    }

    [Fact]
    public async Task Handle_SeasonCrossYearBoundary_CreatesSuccessfully()
    {
        // Arrange - Season that spans year boundary
        var command = new CreateSeasonCommand
        {
            SeasonName = "Year-End Season",
            StartDate = "11/1",
            EndDate = "2/28"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Test Season",
            StartDate = "1/1",
            EndDate = "3/31"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ThrowsAsync(new Exception("Database connection error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Database connection error"));
    }

    [Fact]
    public async Task Handle_VietnameseSeasonName_CreatesCorrectly()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Vụ Đông Xuân",
            StartDate = "12/1",
            EndDate = "4/30"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SeasonWithoutDescription_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Quick Season",
            StartDate = "5/1",
            EndDate = "7/31"
        };

        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CreatedSeason_SetsIsActiveToTrue()
    {
        // Arrange
        var command = new CreateSeasonCommand
        {
            SeasonName = "Active Season",
            StartDate = "1/1",
            EndDate = "4/30"
        };

        Season? capturedSeason = null;
        _mockSeasonRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Season, bool>>>()))
            .ReturnsAsync((Season?)null);

        _mockSeasonRepo.Setup(r => r.AddAsync(It.IsAny<Season>()))
            .Callback<Season>(s => capturedSeason = s)
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        capturedSeason.Should().NotBeNull();
        capturedSeason!.IsActive.Should().BeTrue();
    }
}

