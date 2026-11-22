using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.PlotFeature.Commands.CreatePlots;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.PlotFeature;

/// <summary>
/// Tests for CreatePlotsCommandHandler - validates plot creation logic
/// </summary>
public class CreatePlotsCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator = new();
    private readonly Mock<ILogger<CreatePlotsCommandHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ValidSinglePlot_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Test Farmer", FarmCode = "FARM001" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

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

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        var savedPlots = await context.Plots.ToListAsync();
        savedPlots.Should().ContainSingle(p => p.Area == 1000m && p.FarmerId == farmerId);
    }

    [Fact]
    public async Task Handle_FarmerNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);
        var farmerId = Guid.NewGuid();

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains($"Farmer with ID '{farmerId}' not found"));
    }

    [Fact]
    public async Task Handle_MultiplePlots_CreatesAllSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Test Farmer", FarmCode = "FARM001" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m, SoilType = "Loamy" },
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 2, SoTo = 1, Area = 1500m, SoilType = "Clay" },
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 3, SoTo = 1, Area = 2000m, SoilType = "Sandy" }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        (await context.Plots.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Handle_DuplicatePlot_SkipsDuplicate()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Test Farmer", FarmCode = "FARM001" });
        context.Plots.Add(new Plot
        {
            Id = Guid.NewGuid(),
            FarmerId = farmerId,
            SoThua = 1,
            SoTo = 1,
            Area = 1000m
        });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No plots to create") && e.Contains("All plots already exist"));
        (await context.Plots.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_EmptyPlotList_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No plots to create"));
    }

    [Fact]
    public async Task Handle_PlotWithZeroArea_ReturnsValidationError()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Test Farmer" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 0m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Area") || e.Contains("must be greater than"));
    }

    [Fact]
    public async Task Handle_PlotWithNegativeArea_ReturnsValidationError()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Test Farmer" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = -100m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Area") || e.Contains("must be greater than"));
    }

    [Fact]
    public async Task Handle_LargeNumberOfPlots_HandlesEfficiently()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Large Scale Farmer" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

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

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(50);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Test Farmer" });
        await context.SaveChangesAsync();
        context.SaveChangesException = new Exception("Database error");

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Database error"));
    }

    [Fact]
    public async Task Handle_PlotWithVeryLargeArea_CreatesSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Large Estate Farmer" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 100000m, SoilType = "Mixed" }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.First().Area.Should().Be(100000m);
    }

    [Fact]
    public async Task Handle_PlotsWithDifferentSoilTypes_CreatesWithCorrectTypes()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Diverse Farm" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

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

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(4);
        result.Data!.Select(p => p.SoilType).Should().Contain(new[] { "Loamy", "Clay", "Sandy", "Silt" });
    }

    [Fact]
    public async Task Handle_PlotStatusSetToPending_CreatesWithPendingStatus()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FullName = "Test Farmer" });
        await context.SaveChangesAsync();

        var handler = new CreatePlotsCommandHandler(context, _mockMediator.Object, _mockLogger.Object);

        var command = new CreatePlotsCommand
        {
            Plots = new List<PlotCreationRequest>
            {
                new PlotCreationRequest { FarmerId = farmerId, SoThua = 1, SoTo = 1, Area = 1000m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.First().Status.Should().Be(PlotStatus.PendingPolygon);
    }
}

