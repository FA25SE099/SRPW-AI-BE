using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.PlotFeature.Commands.ImportExcel;
using RiceProduction.Application.PlotFeature.Events;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.PlotFeature;

/// <summary>
/// Tests for ImportPlotByExcelCommandHandler - validates Excel import functionality
/// </summary>
public class ImportPlotByExcelCommandHandlerTests
{
    private readonly Mock<IGenericExcel> _mockGenericExcel = new();
    private readonly Mock<IMediator> _mockMediator = new();
    private readonly Mock<ILogger<ImportPlotByExcelCommandHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_EmptyExcelFile_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(new List<PlotImportRow>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The uploaded Excel file is empty or invalid.");
    }

    [Fact]
    public async Task Handle_MissingFarmCode_ReturnsValidationError()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = null!,
                SoThua = 1,
                SoTo = 1,
                Area = 1000m
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("FarmCode is required"));
    }

    [Fact]
    public async Task Handle_FarmerNotFound_ReturnsValidationError()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = "FARM001",
                SoThua = 1,
                SoTo = 1,
                Area = 1000m
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Farmer 'FARM001' not found"));
    }

    [Fact]
    public async Task Handle_InvalidSoThua_ReturnsValidationError()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        context.Farmers.Add(new Farmer { Id = Guid.NewGuid(), FarmCode = "FARM001", FullName = "John Farmer" });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = "FARM001",
                SoThua = 0, // Invalid
                SoTo = 1,
                Area = 1000m
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("SoThua is required and must be > 0"));
    }

    [Fact]
    public async Task Handle_InvalidArea_ReturnsValidationError()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        context.Farmers.Add(new Farmer { Id = Guid.NewGuid(), FarmCode = "FARM001", FullName = "John Farmer" });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = "FARM001",
                SoThua = 1,
                SoTo = 1,
                Area = -100m // Invalid negative area
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Area is required and must be > 0"));
    }

    [Fact]
    public async Task Handle_ValidPlotData_WithoutRiceVariety_CreatesPlotSuccessfully()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = "FARM001",
                SoThua = 1,
                SoTo = 1,
                Area = 1000m,
                SoilType = "Loamy"
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data!.First().Area.Should().Be(1000m);
        result.Data!.First().Status.Should().Be(PlotStatus.PendingPolygon);

        var savedPlots = await context.Plots.ToListAsync();
        savedPlots.Should().ContainSingle(p => p.Area == 1000m && p.FarmerId == farmerId);
    }

    [Fact]
    public async Task Handle_ValidPlotData_WithRiceVariety_CreatesPlotAndCultivation()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        var riceVarietyId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        context.Farmers.Add(new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" });
        context.RiceVarieties.Add(new RiceVariety { Id = riceVarietyId, VarietyName = "IR64" });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            SeasonName = "Winter-Spring",
            StartDate = "01/01",
            EndDate = "12/31",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = "FARM001",
                SoThua = 1,
                SoTo = 1,
                Area = 1000m,
                RiceVarietyName = "IR64"
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);

        var savedCultivations = await context.PlotCultivations.ToListAsync();
        savedCultivations.Should().ContainSingle(c => c.RiceVarietyId == riceVarietyId);

        var savedVersions = await context.CultivationVersions.ToListAsync();
        savedVersions.Should().ContainSingle(v => v.IsActive);
    }

    [Fact]
    public async Task Handle_DuplicatePlot_SkipsDuplicate()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" });
        context.Plots.Add(new Plot
        {
            Id = Guid.NewGuid(),
            FarmerId = farmerId,
            SoThua = 1,
            SoTo = 1,
            Area = 1000m
        });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = "FARM001",
                SoThua = 1,
                SoTo = 1,
                Area = 1000m
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty(); // No new plots created
        (await context.Plots.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_RiceVarietyNotFound_ReturnsValidationError()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        context.Farmers.Add(new Farmer { Id = Guid.NewGuid(), FarmCode = "FARM001", FullName = "John Farmer" });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = "FARM001",
                SoThua = 1,
                SoTo = 1,
                Area = 1000m,
                RiceVarietyName = "UnknownVariety"
            }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Rice variety 'UnknownVariety' not found"));
    }

    [Fact]
    public async Task Handle_MultipleValidPlots_CreatesAllPlots()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow { FarmCode = "FARM001", SoThua = 1, SoTo = 1, Area = 1000m },
            new PlotImportRow { FarmCode = "FARM001", SoThua = 2, SoTo = 1, Area = 1500m },
            new PlotImportRow { FarmCode = "FARM001", SoThua = 3, SoTo = 1, Area = 2000m }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        result.Message.Should().Contain("Successfully imported 3 plots");
        (await context.Plots.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Handle_ExceptionDuringProcessing_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ThrowsAsync(new Exception("Excel processing error"));

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Import failed"));
    }

    [Fact]
    public async Task Handle_EmptyRowsSkipped_CountsCorrectly()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow { FarmCode = "FARM001", SoThua = 1, SoTo = 1, Area = 1000m },
            new PlotImportRow { }, // Empty row
            new PlotImportRow { FarmCode = "FARM001", SoThua = 2, SoTo = 1, Area = 1500m }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2); // Only 2 valid plots
        result.Message.Should().Contain("1 empty rows skipped");
    }

    [Fact]
    public async Task Handle_SuccessfulImport_PublishesEvent()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var farmerId = Guid.NewGuid();
        var clusterManagerId = Guid.NewGuid();
        context.Farmers.Add(new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" });
        await context.SaveChangesAsync();

        var handler = new ImportPlotByExcelCommandHandler(
            context,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);

        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow { FarmCode = "FARM001", SoThua = 1, SoTo = 1, Area = 1000m }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = clusterManagerId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();

        _mockMediator.Verify(m => m.Publish(
            It.Is<PlotImportedEvent>(e =>
                e.ClusterManagerId == clusterManagerId &&
                e.TotalPlotsImported == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

