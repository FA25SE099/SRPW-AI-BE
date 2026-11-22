using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.FarmLogFeature;

/// <summary>
/// Tests for CreateFarmLogCommandHandler - validates farm log creation logic
/// </summary>
public class CreateFarmLogCommandHandlerTests
{
    private readonly Mock<IStorageService> _mockStorageService;

    public CreateFarmLogCommandHandlerTests()
    {
        _mockStorageService = new Mock<IStorageService>();
    }

    private CreateFarmLogCommandHandler CreateHandler(TestApplicationDbContext context) =>
        new(
            context,
            _mockStorageService.Object,
            Mock.Of<ILogger<CreateFarmLogCommandHandler>>());

    private static async Task<(Guid TaskId, Guid PlotCultivationId, Guid VersionId)> SeedTaskWithActiveVersionAsync(
        TestApplicationDbContext context,
        Guid? taskVersionId = null)
    {
        var plotCultivationId = Guid.NewGuid();
        var activeVersionId = Guid.NewGuid();
        var cultivationTaskId = Guid.NewGuid();

        var plotCultivation = MockDataBuilder.CreatePlotCultivation(
            id: plotCultivationId,
            plotId: Guid.NewGuid(),
            seasonId: Guid.NewGuid(),
            riceVarietyId: Guid.NewGuid(),
            status: CultivationStatus.InProgress);

        var activeVersion = new CultivationVersion
        {
            Id = activeVersionId,
            PlotCultivationId = plotCultivationId,
            IsActive = true,
            VersionOrder = 1
        };

        var task = MockDataBuilder.CreateCultivationTask(
            id: cultivationTaskId,
            executionOrder: 1,
            status: RiceProduction.Domain.Enums.TaskStatus.Draft,
            plotCultivationId: plotCultivationId,
            taskType: TaskType.PestControl,
            scheduledEndDate: DateTime.UtcNow.AddDays(5));
        task.VersionId = taskVersionId ?? activeVersionId;

        context.PlotCultivations.Add(plotCultivation);
        context.CultivationVersions.Add(activeVersion);
        context.CultivationTasks.Add(task);
        await context.SaveChangesAsync();

        return (cultivationTaskId, plotCultivationId, activeVersionId);
    }

    [Fact]
    public async Task Handle_TaskNotFound_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);

        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = Guid.NewGuid(),
            PlotCultivationId = Guid.NewGuid(),
            FarmerId = Guid.NewGuid(),
            WorkDescription = "Test work"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Task not found"));
    }

    [Fact]
    public async Task Handle_VersionConflict_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var (taskId, plotCultivationId, _) = await SeedTaskWithActiveVersionAsync(context, taskVersionId: Guid.NewGuid());
        var handler = CreateHandler(context);

        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = taskId,
            PlotCultivationId = plotCultivationId,
            FarmerId = Guid.NewGuid(),
            WorkDescription = "Test work"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("active plan version"));
    }

    [Fact]
    public async Task Handle_ValidCommand_WithoutMaterials_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var (taskId, plotCultivationId, _) = await SeedTaskWithActiveVersionAsync(context);
        var farmerId = Guid.NewGuid();
        var handler = CreateHandler(context);

        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = taskId,
            PlotCultivationId = plotCultivationId,
            FarmerId = farmerId,
            WorkDescription = "Completed pest control",
            ActualAreaCovered = 1000M,
            ServiceCost = 500000M,
            WeatherConditions = "Sunny"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        var log = await context.FarmLogs.SingleOrDefaultAsync(l => l.Id == result.Data);
        log.Should().NotBeNull();
        log!.CultivationTaskId.Should().Be(taskId);
        log.PlotCultivationId.Should().Be(plotCultivationId);
        log.LoggedBy.Should().Be(farmerId);
        log.ServiceCost.Should().Be(500000M);

        var updatedTask = await context.CultivationTasks.SingleAsync(t => t.Id == taskId);
        updatedTask.Status.Should().Be(RiceProduction.Domain.Enums.TaskStatus.Completed);
    }

    [Fact]
    public async Task Handle_ValidCommand_WithMaterialsAndImages_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var (taskId, plotCultivationId, _) = await SeedTaskWithActiveVersionAsync(context);
        var farmerId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        context.MaterialPrices.Add(new MaterialPrice
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            PricePerMaterial = 50000M,
            ValidFrom = DateTime.UtcNow.AddDays(-30).Date,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024);
        mockFile.Setup(f => f.FileName).Returns("test.jpg");

        _mockStorageService.Setup(s => s.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(("https://storage.example.com/test.jpg", "test.jpg"));

        var handler = CreateHandler(context);
        var command = new CreateFarmLogCommand
        {
            CultivationTaskId = taskId,
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

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        _mockStorageService.Verify(s => s.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Once);

        var log = await context.FarmLogs
            .Include(l => l.FarmLogMaterials)
            .SingleAsync(l => l.Id == result.Data);
        log.CultivationTaskId.Should().Be(taskId);
        log.PlotCultivationId.Should().Be(plotCultivationId);
        log.LoggedBy.Should().Be(farmerId);
        log.FarmLogMaterials.Should().HaveCount(1);
        log.PhotoUrls.Should().NotBeNull().And.HaveCount(1);

        var updatedTask = await context.CultivationTasks.SingleAsync(t => t.Id == taskId);
        updatedTask.ActualMaterialCost.Should().BeGreaterThan(0);
        updatedTask.ActualServiceCost.Should().Be(200000M);
    }

    [Fact]
    public async Task Constructor_InitializesAllDependencies()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);

        handler.Should().NotBeNull();
    }
}
