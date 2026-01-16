using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.PlotFeature.Commands.ImportExcel;
using RiceProduction.Application.PlotFeature.Events;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;
using Xunit;
using MediatR;

namespace RiceProduction.Tests.Unit.Application.PlotFeature;

/// <summary>
/// Tests for ImportPlotByExcelCommandHandler - validates Excel import functionality
/// </summary>
public class ImportPlotByExcelCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericExcel> _mockGenericExcel;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<ImportPlotByExcelCommandHandler>> _mockLogger;
    private readonly Mock<IGenericRepository<Plot>> _mockPlotRepo;
    private readonly Mock<IFarmerRepository> _mockFarmerRepo;
    private readonly Mock<IGenericRepository<RiceVariety>> _mockRiceVarietyRepo;
    private readonly Mock<IGenericRepository<Season>> _mockSeasonRepo;
    private readonly Mock<IGenericRepository<PlotCultivation>> _mockPlotCultivationRepo;
    private readonly Mock<IGenericRepository<CultivationVersion>> _mockCultivationVersionRepo;
    private readonly ImportPlotByExcelCommandHandler _handler;

    public ImportPlotByExcelCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockGenericExcel = new Mock<IGenericExcel>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<ImportPlotByExcelCommandHandler>>();
        _mockPlotRepo = new Mock<IGenericRepository<Plot>>();
        _mockFarmerRepo = new Mock<IFarmerRepository>();
        _mockRiceVarietyRepo = new Mock<IGenericRepository<RiceVariety>>();
        _mockSeasonRepo = new Mock<IGenericRepository<Season>>();
        _mockPlotCultivationRepo = new Mock<IGenericRepository<PlotCultivation>>();
        _mockCultivationVersionRepo = new Mock<IGenericRepository<CultivationVersion>>();

        _mockUnitOfWork.Setup(u => u.Repository<Plot>()).Returns(_mockPlotRepo.Object);
        _mockUnitOfWork.Setup(u => u.FarmerRepository).Returns(_mockFarmerRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<RiceVariety>()).Returns(_mockRiceVarietyRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Season>()).Returns(_mockSeasonRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<PlotCultivation>()).Returns(_mockPlotCultivationRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<CultivationVersion>()).Returns(_mockCultivationVersionRepo.Object);

        _handler = new ImportPlotByExcelCommandHandler(
            _mockUnitOfWork.Object,
            _mockGenericExcel.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_EmptyExcelFile_ReturnsFailure()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(new List<PlotImportRow>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The uploaded Excel file is empty or invalid.");
    }

    [Fact]
    public async Task Handle_MissingFarmCode_ReturnsValidationError()
    {
        // Arrange
        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow
            {
                FarmCode = null,
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
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("FarmCode is required"));
    }

    [Fact]
    public async Task Handle_FarmerNotFound_ReturnsValidationError()
    {
        // Arrange
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

        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Farmer 'FARM001' not found"));
    }

    [Fact]
    public async Task Handle_InvalidSoThua_ReturnsValidationError()
    {
        // Arrange
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

        var farmer = new Farmer { Id = Guid.NewGuid(), FarmCode = "FARM001" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("SoThua is required and must be > 0"));
    }

    [Fact]
    public async Task Handle_InvalidArea_ReturnsValidationError()
    {
        // Arrange
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

        var farmer = new Farmer { Id = Guid.NewGuid(), FarmCode = "FARM001" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Area is required and must be > 0"));
    }

    [Fact]
    public async Task Handle_ValidPlotData_WithoutRiceVariety_CreatesPlotSuccessfully()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
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

        var farmer = new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockFarmerRepo.Setup(r => r.GetFarmerByIdAsync(farmerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(farmer);

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null))
            .ReturnsAsync((Plot?)null);

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data.First().Area.Should().Be(1000m);
        result.Data.First().Status.Should().Be(PlotStatus.PendingPolygon);
        
        _mockPlotRepo.Verify(r => r.AddRangeAsync(It.Is<List<Plot>>(
            plots => plots.Count == 1 && plots[0].Area == 1000m)), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidPlotData_WithRiceVariety_CreatesPlotAndCultivation()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var riceVarietyId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        
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

        var farmer = new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockFarmerRepo.Setup(r => r.GetFarmerByIdAsync(farmerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(farmer);

        var riceVariety = new RiceVariety { Id = riceVarietyId, VarietyName = "IR64" };
        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety> { riceVariety });

        var season = new Season 
        { 
            Id = seasonId, 
            SeasonName = "Winter-Spring",
            StartDate = "1/1",
            EndDate = "5/31"
        };
        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season> { season });

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null))
            .ReturnsAsync((Plot?)null);

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        
        _mockPlotCultivationRepo.Verify(r => r.AddRangeAsync(It.Is<List<PlotCultivation>>(
            cultivations => cultivations.Count == 1 && 
                           cultivations[0].RiceVarietyId == riceVarietyId)), Times.Once);
                           
        _mockCultivationVersionRepo.Verify(r => r.AddRangeAsync(It.Is<List<CultivationVersion>>(
            versions => versions.Count == 1 && 
                       versions[0].IsActive == true)), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicatePlot_SkipsDuplicate()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
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

        var farmer = new Farmer { Id = farmerId, FarmCode = "FARM001" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        // Existing plot
        var existingPlot = new Plot 
        { 
            Id = Guid.NewGuid(), 
            FarmerId = farmerId,
            SoThua = 1,
            SoTo = 1
        };
        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(existingPlot);

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty(); // No plots created
        _mockPlotRepo.Verify(r => r.AddRangeAsync(It.IsAny<List<Plot>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RiceVarietyNotFound_ReturnsValidationError()
    {
        // Arrange
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

        var farmer = new Farmer { Id = Guid.NewGuid(), FarmCode = "FARM001" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockRiceVarietyRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<RiceVariety, bool>>>(), null, null))
            .ReturnsAsync(new List<RiceVariety>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Rice variety 'UnknownVariety' not found"));
    }

    [Fact]
    public async Task Handle_MultipleValidPlots_CreatesAllPlots()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow { FarmCode = "FARM001", SoThua = 1, SoTo = 1, Area = 1000m },
            new PlotImportRow { FarmCode = "FARM001", SoThua = 2, SoTo = 1, Area = 1500m },
            new PlotImportRow { FarmCode = "FARM001", SoThua = 3, SoTo = 1, Area = 2000m }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var farmer = new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockFarmerRepo.Setup(r => r.GetFarmerByIdAsync(farmerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(farmer);

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null))
            .ReturnsAsync((Plot?)null);

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        result.Message.Should().Contain("Successfully imported 3 plots");
    }

    [Fact]
    public async Task Handle_ExceptionDuringProcessing_ReturnsFailure()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ThrowsAsync(new Exception("Excel processing error"));

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Import failed"));
    }

    [Fact]
    public async Task Handle_EmptyRowsSkipped_CountsCorrectly()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow { FarmCode = "FARM001", SoThua = 1, SoTo = 1, Area = 1000m },
            new PlotImportRow { }, // Empty row
            new PlotImportRow { FarmCode = "FARM001", SoThua = 2, SoTo = 1, Area = 1500m }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var farmer = new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockFarmerRepo.Setup(r => r.GetFarmerByIdAsync(farmerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(farmer);

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null))
            .ReturnsAsync((Plot?)null);

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2); // Only 2 valid plots
        result.Message.Should().Contain("1 empty rows skipped");
    }

    [Fact]
    public async Task Handle_SuccessfulImport_PublishesEvent()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var clusterManagerId = Guid.NewGuid();
        var plotImportRows = new List<PlotImportRow>
        {
            new PlotImportRow { FarmCode = "FARM001", SoThua = 1, SoTo = 1, Area = 1000m }
        };

        var mockFile = new Mock<IFormFile>();
        _mockGenericExcel.Setup(e => e.ExcelToListT<PlotImportRow>(It.IsAny<IFormFile>()))
            .ReturnsAsync(plotImportRows);

        var farmer = new Farmer { Id = farmerId, FarmCode = "FARM001", FullName = "John Farmer" };
        _mockFarmerRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Farmer, bool>>>(), null, null))
            .ReturnsAsync(new List<Farmer> { farmer });

        _mockFarmerRepo.Setup(r => r.GetFarmerByIdAsync(farmerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(farmer);

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>(), null))
            .ReturnsAsync((Plot?)null);

        _mockPlotRepo.Setup(r => r.GenerateNewGuid(It.IsAny<Guid>()))
            .ReturnsAsync(Guid.NewGuid());

        _mockSeasonRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Season, bool>>>(), null, null))
            .ReturnsAsync(new List<Season>());

        var command = new ImportPlotByExcelCommand
        {
            ExcelFile = mockFile.Object,
            ClusterManagerId = clusterManagerId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        
        _mockMediator.Verify(m => m.Publish(
            It.Is<PlotImportedEvent>(e => 
                e.ClusterManagerId == clusterManagerId && 
                e.TotalPlotsImported == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

