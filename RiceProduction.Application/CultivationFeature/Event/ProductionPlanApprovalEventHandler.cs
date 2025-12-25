using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.CultivationFeature.Event;
using RiceProduction.Domain.Entities;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

public class ProductionPlanApprovalEventHandler : INotificationHandler<ProductionPlanApprovalEvent>
    {
        private readonly IGenericRepository<ProductionPlan> _planRepo;
        private readonly IGenericRepository<MaterialPrice> _priceRepo;
        private readonly IGenericRepository<Material> _materialRepo;
        private readonly IGenericRepository<CultivationTask> _taskRepo;
        private readonly IGenericRepository<CultivationTaskMaterial> _materialTaskRepo;
        private readonly IGenericRepository<CultivationVersion> _versionRepo;
        private readonly ILogger<ProductionPlanApprovalEventHandler> _logger;

        public ProductionPlanApprovalEventHandler(
            IGenericRepository<ProductionPlan> planRepo,
            IGenericRepository<MaterialPrice> priceRepo,
            IGenericRepository<Material> materialRepo,
            IGenericRepository<CultivationTask> taskRepo,
            IGenericRepository<CultivationTaskMaterial> materialTaskRepo,
            IGenericRepository<CultivationVersion> versionRepo,
            ILogger<ProductionPlanApprovalEventHandler> logger)
        {
            _planRepo = planRepo;
            _priceRepo = priceRepo;
            _materialRepo = materialRepo;
            _taskRepo = taskRepo;
            _materialTaskRepo = materialTaskRepo;
            _versionRepo = versionRepo;
            _logger = logger;
        }

        public async Task Handle(ProductionPlanApprovalEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProductionPlanApprovalEvent received for Plan ID: {PlanId}", notification.PlanId);
            
            //Get all plan with include
            var plan = await FetchPlanWithRelationsAsync(notification.PlanId, cancellationToken);
            if (!IsValidForTaskCreation(plan))
            {
                _logger.LogWarning("No stages found for approved plan {PlanId}", notification.PlanId);
                return;
            }
            
            // FIX #1: Check if tasks already exist for this plan to prevent duplicate creation
            // Get all ProductionPlanTask IDs from this plan
            var planTaskIds = plan.CurrentProductionStages
                .SelectMany(s => s.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
                .Select(t => t.Id)
                .ToList();
            
            if (planTaskIds.Any())
            {
                // Check if any CultivationTasks already exist for these ProductionPlanTasks
                var existingTasksCount = await _taskRepo.GetQueryable()
                    .Where(ct => ct.ProductionPlanTaskId != null && planTaskIds.Contains(ct.ProductionPlanTaskId.Value))
                    .CountAsync(cancellationToken);
                
                if (existingTasksCount > 0)
                {
                    _logger.LogWarning(
                        "Tasks already exist for plan {PlanId} ({ExistingCount} tasks found). Skipping task creation to prevent duplicates.",
                        notification.PlanId, existingTasksCount);
                    return;
                }
            }
            
            //Populate material Lookup
            var (materialDict, priceDict) = await LoadMaterialsAndPricesAsync(plan, cancellationToken);
            if (!materialDict.Any())
            {
                _logger.LogInformation("No materials found for plan {PlanId}", notification.PlanId);
                return;
            }
            
            //Populate plotcultivation Lookup
            var plots = plan.Group!.GroupPlots.Select(gp => gp.Plot).ToList();
            
            // FIX #2: Get only plot cultivations for the current season to avoid duplicates
            var seasonId = plan.Group.SeasonId;
            
            if (!seasonId.HasValue)
            {
                _logger.LogWarning("Group {GroupId} for plan {PlanId} has no season assigned", 
                    plan.Group.Id, notification.PlanId);
                return;
            }
            
            var plotCultivations = plots
                .SelectMany(pl => pl.PlotCultivations)
                .Where(pc => pc.SeasonId == seasonId.Value) // Only current season
                .GroupBy(pc => pc.PlotId) // Group by PlotId
                .Select(g => g.OrderByDescending(pc => pc.CreatedAt).First()) // Take latest per plot
                .ToList();
            
            if (!plotCultivations.Any())
            {
                _logger.LogWarning("No plot cultivations found for plan {PlanId} in season {SeasonId}", 
                    notification.PlanId, seasonId.Value);
                return;
            }
            
            _logger.LogInformation(
                "Processing {PlotCount} unique plots for plan {PlanId}", 
                plotCultivations.Count, notification.PlanId);
            
            // FIX #3: Create ONE version per plot cultivation (not per task!)
            // This creates exactly one version for each plot cultivation
            var versionLookup = new Dictionary<Guid, Guid>();
            
            foreach (var plotCultivation in plotCultivations)
            {
                var newVersion = new CultivationVersion
                {
                    Id = await _versionRepo.GenerateNewGuid(Guid.NewGuid()),
                    VersionName = "0",
                    Reason = "Initial version created upon plan approval",
                    ActivatedAt = DateTime.UtcNow,
                    PlotCultivationId = plotCultivation.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    VersionOrder = 1,
                };
                await _versionRepo.AddAsync(newVersion);
                versionLookup[plotCultivation.Id] = newVersion.Id;
                
                _logger.LogInformation(
                    "Created version {VersionId} for PlotCultivation {PlotCultivationId}",
                    newVersion.Id, plotCultivation.Id);
            }
            
            // Save all versions at once
            await _versionRepo.SaveChangesAsync();
            
            _logger.LogInformation(
                "Created {VersionCount} versions for {PlotCount} plot cultivations", 
                versionLookup.Count, plotCultivations.Count);
            
            var cultivationTasks = new List<CultivationTask>();
            var cultivationTaskMaterials = new List<CultivationTaskMaterial>();

            // Now create tasks - each task references the single version for its plot cultivation
            foreach (var stage in plan.CurrentProductionStages)
            {
                foreach (var planTask in stage.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
                {
                    foreach (var plotCultivation in plotCultivations)
                    {
                        if (plotCultivation.Area <= 0)
                        {
                            _logger.LogWarning("Skipping zero-area plot {PlotId} for task {TaskId}", 
                                plotCultivation.PlotId, planTask.Id);
                            continue;
                        }

                        // Use the pre-created version for this plot cultivation
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
            }

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
            Func<IQueryable<ProductionPlan>, IIncludableQueryable<ProductionPlan, object>> includes =
                q => q.Include(p => p.CurrentProductionStages)
                      .ThenInclude(s => s.ProductionPlanTasks)
                      .ThenInclude(t => t.ProductionPlanTaskMaterials)
                      .Include(p => p.Group)
                      .ThenInclude(g => g.GroupPlots)
                      .ThenInclude(gp => gp.Plot)
                      .ThenInclude(pl => pl.PlotCultivations);

            var plans = await _planRepo.ListAsync(
                filter: p => p.Id == planId,
                includeProperties: includes
                );
            return plans.FirstOrDefault();
        }

        private static bool IsValidForTaskCreation(ProductionPlan? plan)
        {
            return plan != null && plan.CurrentProductionStages.Any();
        }

        private async Task<(Dictionary<Guid, Material> MaterialDict, Dictionary<Guid, MaterialPrice> PriceDict)> LoadMaterialsAndPricesAsync(
            ProductionPlan plan, CancellationToken cancellationToken)
        {
            var materialIds = plan.CurrentProductionStages
                .SelectMany(s => s.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
                .SelectMany(t => t.ProductionPlanTaskMaterials ?? Enumerable.Empty<ProductionPlanTaskMaterial>())
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            if (!materialIds.Any()) return (new Dictionary<Guid, Material>(), new Dictionary<Guid, MaterialPrice>());

            var now = DateTime.UtcNow;

            var materials = await _materialRepo.ListAsync(
                filter: m => materialIds.Contains(m.Id)
                );
            var materialDict = materials.ToDictionary(m => m.Id);

            var currentPrices = await _priceRepo.ListAsync(
                filter: mp => materialIds.Contains(mp.MaterialId)
                           && mp.ValidFrom <= now
                           && (!mp.ValidTo.HasValue || mp.ValidTo > now),
                orderBy: q => q.OrderByDescending(mp => mp.ValidFrom)
                );
            var priceDict = currentPrices
                .GroupBy(p => p.MaterialId)
                .ToDictionary(g => g.Key, g => g.First()); 

            return (materialDict, priceDict);
        }

    private (CultivationTask Task, List<CultivationTaskMaterial> TaskMaterials) CreateTaskForPlot(
    ProductionPlanTask planTask, PlotCultivation plotCultivation, Guid versionId,
    Dictionary<Guid, MaterialPrice> priceDict, Dictionary<Guid, Material> materialDict)
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
            var requiredQuantity = planTaskMaterial.QuantityPerHa * plotCultivation.Area;

            // Calculate packs needed with ceiling (round up to full units)
            var packsNeeded = Math.Ceiling((decimal)(requiredQuantity / amountPerUnit));
            var actualCost = packsNeeded * currentPrice.PricePerMaterial;

            var taskMaterial = new CultivationTaskMaterial
            {
                Id = Guid.NewGuid(),
                CultivationTaskId = task.Id,
                MaterialId = planTaskMaterial.MaterialId,
                ActualQuantity = (decimal)requiredQuantity,
                ActualCost = actualCost,
                Notes = $"Adjusted for {plotCultivation.Area} ha from plan estimate (required {requiredQuantity}, packs: {packsNeeded})."
            };
            taskMaterials.Add(taskMaterial);
            totalMaterialCost += actualCost;
        }

        task.ActualMaterialCost = totalMaterialCost;
        return (task, taskMaterials);
    }

    private async Task SaveTasksAndMaterialsAsync(
            List<CultivationTask> tasks, List<CultivationTaskMaterial> materials, CancellationToken cancellationToken)
        {
            if (!tasks.Any()) return;

            await _taskRepo.AddRangeAsync(tasks);
            if (materials.Any())
            {
                await _materialTaskRepo.AddRangeAsync(materials);
            }
        await _taskRepo.SaveChangesAsync();
        }
    }

