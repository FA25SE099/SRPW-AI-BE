using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.PlotFeature.Commands.CreatePlots;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using NetTopologySuite.Geometries;
using System.Linq.Expressions;
using Xunit;
using MediatR;

namespace RiceProduction.Tests.Unit.Application.PlotFeature;

/// <summary>
/// Tests for CreatePlotsCommandHandler - validates plot creation logic
/// </summary>
public class CreatePlotsCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Plot>> _mockPlotRepo;
    private readonly Mock<IGenericRepository<Season>> _mockSeasonRepo;
    private readonly Mock<IGenericRepository<RiceVariety>> _mockRiceVarietyRepo;
    private readonly Mock<IGenericRepository<PlotCultivation>> _mockPlotCultivationRepo;
    private readonly Mock<IGenericRepository<CultivationVersion>> _mockCultivationVersionRepo;
    private readonly Mock<IFarmerRepository> _mockFarmerRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreatePlotsCommandHandler>> _mockLogger;
    private readonly CreatePlotsCommandHandler _handler;

    public CreatePlotsCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPlotRepo = new Mock<IGenericRepository<Plot>>();
        _mockSeasonRepo = new Mock<IGenericRepository<Season>>();
        _mockRiceVarietyRepo = new Mock<IGenericRepository<RiceVariety>>();
        _mockPlotCultivationRepo = new Mock<IGenericRepository<PlotCultivation>>();
        _mockCultivationVersionRepo = new Mock<IGenericRepository<CultivationVersion>>();
        _mockFarmerRepo = new Mock<IFarmerRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreatePlotsCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Plot>()).Returns(_mockPlotRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Season>()).Returns(_mockSeasonRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<RiceVariety>()).Returns(_mockRiceVarietyRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<PlotCultivation>()).Returns(_mockPlotCultivationRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<CultivationVersion>()).Returns(_mockCultivationVersionRepo.Object);
        _mockUnitOfWork.Setup(u => u.FarmerRepository).Returns(_mockFarmerRepo.Object);

        _handler = new CreatePlotsCommandHandler(
            _mockUnitOfWork.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidSinglePlot_CreatesSuccessfully()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Test Farmer", FarmCode = "FARM001" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest
                {
                    FarmerId = farmerId,
                    SoThua = 1,
                    SoTo = 1,
                    Area = 1000m,
                    SoilType = "Loamy"
                }
            }
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockPlotRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<Plot>>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        _mockPlotRepo.Verify(r => r.AddRangeAsync(It.Is<List<Plot>>(
            plots => plots.Count == 1 && plots[0].Area == 1000m)), Times.Once);
    }

    [Fact]
    public async Task Handle_FarmerNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = Guid.NewGuid(), SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Farmer not found"));
    }

    [Fact]
    public async Task Handle_MultiplePlots_CreatesAllSuccessfully()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Test Farmer", FarmCode = "FARM001" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m, SoilType = "Loamy" },
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 2, SoTo = 1, Area = 1500m, SoilType = "Clay" },
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 3, SoTo = 1, Area = 2000m, SoilType = "Sandy" }
            }
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        _mockPlotRepo.Verify(r => r.AddRangeAsync(It.Is<List<Plot>>(
            plots => plots.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicatePlot_SkipsDuplicate()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Test Farmer", FarmCode = "FARM001" };

        var existingPlot = new Plot
        {
            Id = Guid.NewGuid(),
            FarmerId = farmerId,
            SoThua = 1,
            SoTo = 1,
            Area = 1000m
        };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        _mockFarmerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null))
            .ReturnsAsync(existingPlot);

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty(); // No new plots created
        result.Message.Should().Contain("skipped");
    }

    [Fact]
    public async Task Handle_EmptyPlotList_ReturnsFailure()
    {
        // Arrange
        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No plots provided") || e.Contains("empty"));
    }

    [Fact]
    public async Task Handle_PlotWithZeroArea_ReturnsValidationError()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Test Farmer" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 0m }
            }
        };

        _mockFarmerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Farmer> { farmer });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Area") || e.Contains("must be greater than"));
    }

    [Fact]
    public async Task Handle_PlotWithNegativeArea_ReturnsValidationError()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Test Farmer" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = -100m }
            }
        };

        _mockFarmerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Farmer> { farmer });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LargeNumberOfPlots_HandlesEfficiently()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Large Scale Farmer" };

        var plots = Enumerable.Range(1, 50)
            .Select(i => new PlotCreationRequest
            {
                FarmerId = farmerId,
                SoThua = i,
                SoTo = 1,
                Area = 1000m + i * 10,
                SoilType = "Mixed"
            })
            .ToList();

        var command = new CreatePlotsCommand
        {
            Plots = plots
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(50);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Test Farmer" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Database error"));
    }

    [Fact]
    public async Task Handle_PlotWithVeryLargeArea_CreatesSuccessfully()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Large Estate Farmer" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 100000m, SoilType = "Mixed" }
            }
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.First().Area.Should().Be(100000m);
    }

    [Fact]
    public async Task Handle_PlotsWithDifferentSoilTypes_CreatesWithCorrectTypes()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Diverse Farm" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m, SoilType = "Loamy" },
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 2, SoTo = 1, Area = 1000m, SoilType = "Clay" },
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 3, SoTo = 1, Area = 1000m, SoilType = "Sandy" },
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 4, SoTo = 1, Area = 1000m, SoilType = "Silt" }
            }
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(4);
        result.Data.Select(p => p.SoilType).Should().Contain(new[] { "Loamy", "Clay", "Sandy", "Silt" });
    }

    [Fact]
    public async Task Handle_PlotStatusSetToPending_CreatesWithPendingStatus()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmer = new Farmer { Id = farmerId, FullName = "Test Farmer" };

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockUnitOfWork.Setup(u => u.CompleteAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.First().Status.Should().Be(PlotStatus.PendingPolygon);
    }
}

