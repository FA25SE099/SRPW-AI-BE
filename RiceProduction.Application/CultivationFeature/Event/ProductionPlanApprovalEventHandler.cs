using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.CultivationFeature.Event;

public class ProductionPlanApprovalEventHandler : INotificationHandler<ProductionPlanApprovalEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ProductionPlanApprovalEventHandler> _logger;

    public ProductionPlanApprovalEventHandler(
        IApplicationDbContext context,
        ILogger<ProductionPlanApprovalEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(ProductionPlanApprovalEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProductionPlanApprovalEvent received for Plan ID: {PlanId}", notification.PlanId);

        var plan = await FetchPlanWithRelationsAsync(notification.PlanId, cancellationToken);
        if (!IsValidForTaskCreation(plan))
        {
            _logger.LogWarning("No stages found for approved plan {PlanId}", notification.PlanId);
            return;
        }

        if (await TasksAlreadyExistAsync(plan!, notification.PlanId, cancellationToken))
        {
            return;
        }

        var (materialDict, priceDict) = await LoadMaterialsAndPricesAsync(plan!, cancellationToken);
        if (!materialDict.Any())
        {
            _logger.LogInformation("No materials found for plan {PlanId}", notification.PlanId);
            return;
        }

        var seasonId = plan!.Group!.YearSeason?.SeasonId;
        if (seasonId == null)
        {
            _logger.LogWarning("Group {GroupId} for plan {PlanId} has no season assigned",
                plan.Group.Id, notification.PlanId);
            return;
        }

        var plotCultivations = GetLatestPlotCultivations(plan, seasonId.Value);
        if (!plotCultivations.Any())
        {
            _logger.LogWarning("No plot cultivations found for plan {PlanId} in season {SeasonId}",
                notification.PlanId, seasonId.Value);
            return;
        }

        _logger.LogInformation(
            "Processing {PlotCount} unique plots for plan {PlanId}",
            plotCultivations.Count, notification.PlanId);

        var versionLookup = await EnsureCultivationVersionsAsync(plotCultivations, cancellationToken);
        var (cultivationTasks, cultivationTaskMaterials) = CreateTasksAndMaterials(
            plan, plotCultivations, versionLookup, priceDict, materialDict);

        if (!cultivationTasks.Any())
        {
            _logger.LogWarning("No cultivation tasks were created for plan {PlanId}", notification.PlanId);
            return;
        }

        await SaveTasksAndMaterialsAsync(cultivationTasks, cultivationTaskMaterials, cancellationToken);

        _logger.LogInformation(
            "Created {TaskCount} cultivation tasks with {MaterialCount} task materials for {PlotCount} plots in plan {PlanId}",
            cultivationTasks.Count, cultivationTaskMaterials.Count, plotCultivations.Count, notification.PlanId);
    }

    private async Task<ProductionPlan?> FetchPlanWithRelationsAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await _context.ProductionPlans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.CurrentProductionStages)
                .ThenInclude(s => s.ProductionPlanTasks)
                    .ThenInclude(t => t.ProductionPlanTaskMaterials)
            .Include(p => p.Group)
                .ThenInclude(g => g!.YearSeason)
            .Include(p => p.Group)
                .ThenInclude(g => g!.GroupPlots)
                    .ThenInclude(gp => gp.Plot)
                        .ThenInclude(pl => pl.PlotCultivations)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
    }

    private static bool IsValidForTaskCreation(ProductionPlan? plan) =>
        plan != null && plan.CurrentProductionStages.Any();

    private async Task<bool> TasksAlreadyExistAsync(
        ProductionPlan plan,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var planTaskIds = plan.CurrentProductionStages
            .SelectMany(s => s.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
            .Select(t => t.Id)
            .ToList();

        if (!planTaskIds.Any())
        {
            return false;
        }

        var existingTasksCount = await _context.CultivationTasks
            .Where(ct => ct.ProductionPlanTaskId != null && planTaskIds.Contains(ct.ProductionPlanTaskId.Value))
            .CountAsync(cancellationToken);

        if (existingTasksCount == 0)
        {
            return false;
        }

        _logger.LogWarning(
            "Tasks already exist for plan {PlanId} ({ExistingCount} tasks found). Skipping task creation to prevent duplicates.",
            planId, existingTasksCount);
        return true;
    }

    private static List<PlotCultivation> GetLatestPlotCultivations(ProductionPlan plan, Guid seasonId) =>
        plan.Group!.GroupPlots
            .Select(gp => gp.Plot)
            .SelectMany(pl => pl.PlotCultivations)
            .Where(pc => pc.SeasonId == seasonId)
            .GroupBy(pc => pc.PlotId)
            .Select(g => g.OrderByDescending(pc => pc.CreatedAt).First())
            .ToList();

    private async Task<Dictionary<Guid, Guid>> EnsureCultivationVersionsAsync(
        List<PlotCultivation> plotCultivations,
        CancellationToken cancellationToken)
    {
        var plotCultivationIds = plotCultivations.Select(pc => pc.Id).Distinct().ToList();
        var existingVersions = await _context.CultivationVersions
            .AsNoTracking()
            .Where(v => plotCultivationIds.Contains(v.PlotCultivationId))
            .OrderByDescending(v => v.VersionOrder)
            .ToListAsync(cancellationToken);

        var latestVersionByPlotCultivation = existingVersions
            .GroupBy(v => v.PlotCultivationId)
            .ToDictionary(g => g.Key, g => g.First());

        var versionLookup = new Dictionary<Guid, Guid>();
        var newVersionsCreated = 0;
        var existingVersionsUsed = 0;

        foreach (var plotCultivation in plotCultivations)
        {
            if (latestVersionByPlotCultivation.TryGetValue(plotCultivation.Id, out var existingVersion))
            {
                versionLookup[plotCultivation.Id] = existingVersion.Id;
                existingVersionsUsed++;

                _logger.LogInformation(
                    "Using existing version {VersionId} (Order: {VersionOrder}, Name: '{VersionName}') for PlotCultivation {PlotCultivationId}",
                    existingVersion.Id, existingVersion.VersionOrder, existingVersion.VersionName, plotCultivation.Id);
                continue;
            }

            var now = DateTime.UtcNow;
            var newVersion = CultivationVersion.CreateNext(
                plotCultivation.Id,
                currentMaxOrder: 0,
                versionName: "0",
                reason: "Version 0 created upon plan approval",
                activateImmediately: true,
                activatedAt: now);
            newVersion.Id = Guid.NewGuid();
            newVersion.CreatedAt = now;
            _context.CultivationVersions.Add(newVersion);
            versionLookup[plotCultivation.Id] = newVersion.Id;
            newVersionsCreated++;

            _logger.LogInformation(
                "Created new version {VersionId} (Version 0) for PlotCultivation {PlotCultivationId}",
                newVersion.Id, plotCultivation.Id);
        }

        if (newVersionsCreated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Version lookup complete: {ExistingCount} existing versions used, {NewCount} new versions created for {PlotCount} plot cultivations",
            existingVersionsUsed, newVersionsCreated, plotCultivations.Count);

        return versionLookup;
    }

    private async Task<(Dictionary<Guid, Material> MaterialDict, Dictionary<Guid, MaterialPrice> PriceDict)> LoadMaterialsAndPricesAsync(
        ProductionPlan plan,
        CancellationToken cancellationToken)
    {
        var materialIds = plan.CurrentProductionStages
            .SelectMany(s => s.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
            .SelectMany(t => t.ProductionPlanTaskMaterials ?? Enumerable.Empty<ProductionPlanTaskMaterial>())
            .Select(m => m.MaterialId)
            .Distinct()
            .ToList();

        if (!materialIds.Any())
        {
            return (new Dictionary<Guid, Material>(), new Dictionary<Guid, MaterialPrice>());
        }

        var now = DateTime.UtcNow;

        var materialDict = await _context.Materials
            .AsNoTracking()
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var currentPrices = await _context.MaterialPrices
            .AsNoTracking()
            .Where(mp => materialIds.Contains(mp.MaterialId)
                      && mp.ValidFrom <= now
                      && (!mp.ValidTo.HasValue || mp.ValidTo > now))
            .OrderByDescending(mp => mp.ValidFrom)
            .ToListAsync(cancellationToken);

        var priceDict = currentPrices
            .GroupBy(p => p.MaterialId)
            .ToDictionary(g => g.Key, g => g.First());

        return (materialDict, priceDict);
    }

    private (List<CultivationTask> Tasks, List<CultivationTaskMaterial> Materials) CreateTasksAndMaterials(
        ProductionPlan plan,
        List<PlotCultivation> plotCultivations,
        Dictionary<Guid, Guid> versionLookup,
        Dictionary<Guid, MaterialPrice> priceDict,
        Dictionary<Guid, Material> materialDict)
    {
        var cultivationTasks = new List<CultivationTask>();
        var cultivationTaskMaterials = new List<CultivationTaskMaterial>();

        var planTasks = plan.CurrentProductionStages
            .SelectMany(s => s.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>());

        foreach (var planTask in planTasks)
        {
            foreach (var plotCultivation in plotCultivations)
            {
                if (plotCultivation.Area <= 0)
                {
                    _logger.LogWarning("Skipping zero-area plot {PlotId} for task {TaskId}",
                        plotCultivation.PlotId, planTask.Id);
                    continue;
                }

                var versionId = versionLookup.GetValueOrDefault(plotCultivation.Id, Guid.Empty);
                if (versionId == Guid.Empty)
                {
                    _logger.LogWarning(
                        "No version found for PlotCultivation {PlotCultivationId}. Task will be created without version.",
                        plotCultivation.Id);
                }

                var (task, taskMaterials) = CreateTaskForPlot(
                    planTask, plotCultivation, versionId, priceDict, materialDict);
                cultivationTasks.Add(task);
                cultivationTaskMaterials.AddRange(taskMaterials);
            }
        }

        return (cultivationTasks, cultivationTaskMaterials);
    }

    private (CultivationTask Task, List<CultivationTaskMaterial> TaskMaterials) CreateTaskForPlot(
        ProductionPlanTask planTask,
        PlotCultivation plotCultivation,
        Guid versionId,
        Dictionary<Guid, MaterialPrice> priceDict,
        Dictionary<Guid, Material> materialDict)
    {
        var task = new CultivationTask
        {
            Id = Guid.NewGuid(),
            ProductionPlanTaskId = planTask.Id,
            PlotCultivationId = plotCultivation.Id,
            VersionId = versionId != Guid.Empty ? versionId : null,
            CultivationTaskName = planTask.TaskName,
            Description = planTask.Description,
            TaskType = planTask.TaskType,
            ScheduledEndDate = planTask.ScheduledEndDate,
            Status = TaskStatus.Approved,
            ExecutionOrder = planTask.SequenceOrder,
            IsContingency = false,
            ActualMaterialCost = 0,
            ActualServiceCost = 0,
        };

        var taskMaterials = new List<CultivationTaskMaterial>();
        decimal totalMaterialCost = 0;

        foreach (var planTaskMaterial in planTask.ProductionPlanTaskMaterials ?? Enumerable.Empty<ProductionPlanTaskMaterial>())
        {
            if (!priceDict.TryGetValue(planTaskMaterial.MaterialId, out var currentPrice) ||
                !materialDict.TryGetValue(planTaskMaterial.MaterialId, out var material))
            {
                _logger.LogWarning("Missing price/material {MaterialId} for task {TaskId} in plan {PlanId}",
                    planTaskMaterial.MaterialId, planTask.Id, planTask.ProductionStage!.ProductionPlanId);
                continue;
            }

            if (currentPrice.PricePerMaterial == 0m)
            {
                _logger.LogWarning("Price is zero for Material ID {MId} on planting date.", planTaskMaterial.MaterialId);
                continue;
            }

            var amountPerUnit = material.AmmountPerMaterial.GetValueOrDefault(1m);
            if (amountPerUnit <= 0m)
            {
                amountPerUnit = 1m;
            }
            var requiredQuantity = planTaskMaterial.QuantityPerHa * plotCultivation.Area;

            // Round up to full material units
            var packsNeeded = Math.Ceiling((decimal)(requiredQuantity / amountPerUnit));
            var actualCost = packsNeeded * currentPrice.PricePerMaterial;

            taskMaterials.Add(new CultivationTaskMaterial
            {
                Id = Guid.NewGuid(),
                CultivationTaskId = task.Id,
                MaterialId = planTaskMaterial.MaterialId,
                ActualQuantity = (decimal)requiredQuantity,
                ActualCost = actualCost,
                Notes = $"Adjusted for {plotCultivation.Area} ha from plan estimate (required {requiredQuantity}, packs: {packsNeeded})."
            });
            totalMaterialCost += actualCost;
        }

        task.ActualMaterialCost = totalMaterialCost;
        return (task, taskMaterials);
    }

    private async Task SaveTasksAndMaterialsAsync(
        List<CultivationTask> tasks,
        List<CultivationTaskMaterial> materials,
        CancellationToken cancellationToken)
    {
        if (!tasks.Any()) return;

        _context.CultivationTasks.AddRange(tasks);
        if (materials.Any())
        {
            _context.CultivationTaskMaterials.AddRange(materials);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

