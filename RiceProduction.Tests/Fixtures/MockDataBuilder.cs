using Bogus;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Tests.Fixtures;

public static class MockDataBuilder
{
    private static readonly Faker _faker = new Faker();

    public static Group CreateGroup(Guid? id = null, string? groupName = null)
    {
        return new Group
        {
            Id = id ?? Guid.NewGuid(),
            GroupName = groupName ?? _faker.Company.CompanyName(),
            Status = GroupStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-180),
            LastModified = DateTimeOffset.UtcNow
        };
    }

    public static Plot CreatePlot(
        Guid? id = null,
        Guid? farmerId = null,
        decimal? area = null,
        PlotStatus status = PlotStatus.Active)
    {
        return new Plot
        {
            Id = id ?? Guid.NewGuid(),
            FarmerId = farmerId ?? Guid.NewGuid(),
            Area = area ?? _faker.Random.Decimal(0.5m, 5m),
            Status = status,
            SoilType = _faker.Lorem.Word(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-90),
            LastModified = DateTimeOffset.UtcNow
        };
    }

    public static PlotCultivation CreatePlotCultivation(
        Guid? id = null,
        Guid? plotId = null,
        Guid? seasonId = null,
        Guid? riceVarietyId = null,
        CultivationStatus status = CultivationStatus.Planned)
    {
        return new PlotCultivation
        {
            Id = id ?? Guid.NewGuid(),
            PlotId = plotId ?? Guid.NewGuid(),
            SeasonId = seasonId ?? Guid.NewGuid(),
            RiceVarietyId = riceVarietyId ?? Guid.NewGuid(),
            Status = status,
            PlantingDate = DateTime.UtcNow.AddDays(-60),
            Area = _faker.Random.Decimal(0.5m, 5m),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
            LastModified = DateTimeOffset.UtcNow
        };
    }

    public static CultivationTask CreateCultivationTask(
        Guid? id = null,
        int? executionOrder = null,
        TaskStatus? status = null,
        Guid? plotCultivationId = null,
        TaskType? taskType = null,
        DateTime? scheduledEndDate = null)
    {
        return new CultivationTask
        {
            Id = id ?? Guid.NewGuid(),
            ExecutionOrder = executionOrder,
            CultivationTaskName = _faker.Lorem.Sentence(3),
            Description = _faker.Lorem.Paragraph(),
            Status = status ?? TaskStatus.Approved,
            TaskType = taskType ?? TaskType.PestControl,
            PlotCultivationId = plotCultivationId ?? Guid.NewGuid(),
            ScheduledEndDate = scheduledEndDate ?? DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModified = DateTimeOffset.UtcNow
        };
    }

    public static UavOrderPlotAssignment CreateUavOrderPlotAssignment(
        Guid? id = null,
        Guid? uavOrderId = null,
        Guid? plotId = null,
        Guid? cultivationTaskId = null,
        TaskStatus status = TaskStatus.Draft)
    {
        return new UavOrderPlotAssignment
        {
            Id = id ?? Guid.NewGuid(),
            UavServiceOrderId = uavOrderId ?? Guid.NewGuid(),
            PlotId = plotId ?? Guid.NewGuid(),
            CultivationTaskId = cultivationTaskId ?? Guid.NewGuid(),
            ServicedArea = _faker.Random.Decimal(0.5m, 5m),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        };
    }
}
