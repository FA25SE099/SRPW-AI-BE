using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using System.Linq.Expressions;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.FarmLogFeature;

/// <summary>
/// Tests for CreateFarmLogCommandHandler - validates farm log creation logic
/// </summary>
public class CreateFarmLogCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<CultivationTask>> _mockCultivationTaskRepo;
    private readonly Mock<IGenericRepository<PlotCultivation>> _mockPlotCultivationRepo;
    private readonly Mock<IGenericRepository<FarmLog>> _mockFarmLogRepo;
    private readonly Mock<IGenericRepository<MaterialPrice>> _mockMaterialPriceRepo;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ILogger<CreateFarmLogCommandHandler>> _mockLogger;
    private readonly CreateFarmLogCommandHandler _handler;

    public CreateFarmLogCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockCultivationTaskRepo = new Mock<IGenericRepository<CultivationTask>>();
        _mockPlotCultivationRepo = new Mock<IGenericRepository<PlotCultivation>>();
        _mockFarmLogRepo = new Mock<IGenericRepository<FarmLog>>();
        _mockMaterialPriceRepo = new Mock<IGenericRepository<MaterialPrice>>();
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<CreateFarmLogCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<CultivationTask>()).Returns(_mockCultivationTaskRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<PlotCultivation>()).Returns(_mockPlotCultivationRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<FarmLog>()).Returns(_mockFarmLogRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<MaterialPrice>()).Returns(_mockMaterialPriceRepo.Object);

        _handler = new CreateFarmLogCommandHandler(
            _mockUnitOfWork.Object,
            _mockStorageService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_TaskNotFound_ReturnsFailure()
    {
        // Arrange
        _mockCultivationTaskRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<CultivationTask, bool>>>()))
            .ReturnsAsync((CultivationTask?)null);

        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = Guid.NewGuid(),
            PlotCultivationId = Guid.NewGuid(),
            FarmerId = Guid.NewGuid(),
            WorkDescription = "Test work"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Task not found"));
    }

    [Fact]
    public async Task Handle_VersionConflict_ReturnsFailure()
    {
        // Arrange
        var cultivationTaskId = Guid.NewGuid();
        var plotCultivationId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var differentVersionId = Guid.NewGuid();

        var task = MockDataBuilder.CreateCultivationTask(
            id: cultivationTaskId,
            executionOrder: 1,
            status: RiceProduction.Domain.Enums.TaskStatus.Draft,
            plotCultivationId: plotCultivationId,
            taskType: TaskType.PestControl,
            scheduledEndDate: DateTime.UtcNow.AddDays(5));
        task.VersionId = differentVersionId; // Different version

        var cultivationVersion = new CultivationVersion
        {
            Id = versionId,
            PlotCultivationId = plotCultivationId,
            IsActive = true,
            VersionOrder = 1
        };

        var plotCultivation = MockDataBuilder.CreatePlotCultivation(
            id: plotCultivationId,
            plotId: Guid.NewGuid(),
            seasonId: Guid.NewGuid(),
            riceVarietyId: Guid.NewGuid(),
            status: CultivationStatus.InProgress);
        plotCultivation.CultivationVersions = new List<CultivationVersion> { cultivationVersion };

        _mockCultivationTaskRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<CultivationTask, bool>>>()))
            .ReturnsAsync(task);

        _mockPlotCultivationRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<PlotCultivation, bool>>>(),
            It.IsAny<Func<IQueryable<PlotCultivation>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<PlotCultivation, object>>>()))
            .ReturnsAsync(plotCultivation);

        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = cultivationTaskId,
            PlotCultivationId = plotCultivationId,
            FarmerId = Guid.NewGuid(),
            WorkDescription = "Test work"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("active plan version"));
    }

    [Fact]
    public async Task Handle_ValidCommand_WithoutMaterials_CreatesSuccessfully()
    {
        // Arrange
        var cultivationTaskId = Guid.NewGuid();
        var plotCultivationId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();

        var task = MockDataBuilder.CreateCultivationTask(
            id: cultivationTaskId,
            executionOrder: 1,
            status: RiceProduction.Domain.Enums.TaskStatus.Draft,
            plotCultivationId: plotCultivationId,
            taskType: TaskType.PestControl,
            scheduledEndDate: DateTime.UtcNow.AddDays(5));
        task.VersionId = versionId;

        var cultivationVersion = new CultivationVersion
        {
            Id = versionId,
            PlotCultivationId = plotCultivationId,
            IsActive = true,
            VersionOrder = 1
        };

        var plotCultivation = MockDataBuilder.CreatePlotCultivation(
            id: plotCultivationId,
            plotId: Guid.NewGuid(),
            seasonId: Guid.NewGuid(),
            riceVarietyId: Guid.NewGuid(),
            status: CultivationStatus.InProgress);
        plotCultivation.CultivationVersions = new List<CultivationVersion> { cultivationVersion };

        _mockCultivationTaskRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<CultivationTask, bool>>>()))
            .ReturnsAsync(task);

        _mockPlotCultivationRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<PlotCultivation, bool>>>(),
            It.IsAny<Func<IQueryable<PlotCultivation>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<PlotCultivation, object>>>()))
            .ReturnsAsync(plotCultivation);

        _mockCultivationTaskRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<CultivationTask, bool>>>(),
            It.IsAny<Func<IQueryable<CultivationTask>, IOrderedQueryable<CultivationTask>>>(), null))
            .ReturnsAsync(new List<CultivationTask>());

        _mockFarmLogRepo.Setup(r => r.AddAsync(It.IsAny<FarmLog>()))
            .Callback<FarmLog>(log => log.Id = Guid.NewGuid())
            .Returns(Task.CompletedTask);

        _mockFarmLogRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = cultivationTaskId,
            PlotCultivationId = plotCultivationId,
            FarmerId = farmerId,
            WorkDescription = "Completed pest control",
            ActualAreaCovered = 1000M,
            ServiceCost = 500000M,
            WeatherConditions = "Sunny"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        
        _mockFarmLogRepo.Verify(r => r.AddAsync(It.Is<FarmLog>(
            log => log.CultivationTaskId == cultivationTaskId &&
                   log.PlotCultivationId == plotCultivationId &&
                   log.LoggedBy == farmerId &&
                   log.ServiceCost == 500000M)),
            Times.Once);

        _mockCultivationTaskRepo.Verify(r => r.Update(It.Is<CultivationTask>(
            t => t.Id == cultivationTaskId &&
                 t.Status == RiceProduction.Domain.Enums.TaskStatus.Completed)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_WithMaterialsAndImages_CreatesSuccessfully()
    {
        // Arrange
        var cultivationTaskId = Guid.NewGuid();
        var plotCultivationId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        var task = MockDataBuilder.CreateCultivationTask(
            id: cultivationTaskId,
            executionOrder: 1,
            status: RiceProduction.Domain.Enums.TaskStatus.Draft,
            plotCultivationId: plotCultivationId,
            taskType: TaskType.PestControl,
            scheduledEndDate: DateTime.UtcNow.AddDays(5));
        task.VersionId = versionId;

        var cultivationVersion = new CultivationVersion
        {
            Id = versionId,
            PlotCultivationId = plotCultivationId,
            IsActive = true,
            VersionOrder = 1
        };

        var plotCultivation = MockDataBuilder.CreatePlotCultivation(
            id: plotCultivationId,
            plotId: Guid.NewGuid(),
            seasonId: Guid.NewGuid(),
            riceVarietyId: Guid.NewGuid(),
            status: CultivationStatus.InProgress);
        plotCultivation.CultivationVersions = new List<CultivationVersion> { cultivationVersion };

        var materialPrice = new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 50000M,
            ValidFrom = DateTime.UtcNow.AddDays(-30).Date,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockCultivationTaskRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<CultivationTask, bool>>>()))
            .ReturnsAsync(task);

        _mockPlotCultivationRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<PlotCultivation, bool>>>(),
            It.IsAny<Func<IQueryable<PlotCultivation>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<PlotCultivation, object>>>()))
            .ReturnsAsync(plotCultivation);

        _mockMaterialPriceRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<MaterialPrice, bool>>>(), null, null))
            .ReturnsAsync(new List<MaterialPrice> { materialPrice });

        _mockCultivationTaskRepo.Setup(r => r.ListAsync(
            It.IsAny<Expression<Func<CultivationTask, bool>>>(),
            It.IsAny<Func<IQueryable<CultivationTask>, IOrderedQueryable<CultivationTask>>>(), null))
            .ReturnsAsync(new List<CultivationTask>());

        // Mock file upload
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024);
        mockFile.Setup(f => f.FileName).Returns("test.jpg");

        _mockStorageService.Setup(s => s.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(("https://storage.example.com/test.jpg", "test.jpg"));

        _mockFarmLogRepo.Setup(r => r.AddAsync(It.IsAny<FarmLog>()))
            .Callback<FarmLog>(log => log.Id = Guid.NewGuid())
            .Returns(Task.CompletedTask);

        _mockFarmLogRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = cultivationTaskId,
            PlotCultivationId = plotCultivationId,
            FarmerId = farmerId,
            WorkDescription = "Applied pesticide",
            ActualAreaCovered = 1000M,
            ServiceCost = 200000M,
            WeatherConditions = "Cloudy",
            ProofImages = new List<IFormFile> { mockFile.Object },
            Materials = new List<FarmLogMaterialRequest>
            {
                new FarmLogMaterialRequest
                {
                    MaterialId = materialId,
                    ActualQuantityUsed = 10M,
                    Notes = "Used for pest control"
                }
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        
        _mockStorageService.Verify(s => s.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Once);
        
        _mockFarmLogRepo.Verify(r => r.AddAsync(It.Is<FarmLog>(
            log => log.CultivationTaskId == cultivationTaskId &&
                   log.PlotCultivationId == plotCultivationId &&
                   log.LoggedBy == farmerId &&
                   log.FarmLogMaterials.Count == 1 &&
                   log.PhotoUrls != null && log.PhotoUrls.Length == 1)),
            Times.Once);

        _mockCultivationTaskRepo.Verify(r => r.Update(It.Is<CultivationTask>(
            t => t.ActualMaterialCost > 0 && // Should have material costs
                 t.ActualServiceCost == 200000M)),
            Times.Once);
    }

    [Fact]
    public void Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var handler = new CreateFarmLogCommandHandler(
            _mockUnitOfWork.Object,
            _mockStorageService.Object,
            _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
