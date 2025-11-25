
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
            //Get all plan with include
            var plan = await FetchPlanWithRelationsAsync(notification.PlanId, cancellationToken);
            if (!IsValidForTaskCreation(plan))
            {
                _logger.LogWarning("No stages found for approved plan {PlanId}", notification.PlanId);
                return;
            }
            //Populate material Lookup
            var (materialDict, priceDict) = await LoadMaterialsAndPricesAsync(plan, cancellationToken);
            if (!materialDict.Any())
            {
                _logger.LogInformation("No materials found for plan {PlanId}", notification.PlanId);
                return;
            }
            //Populate plotcultivation Lookup
            var plotCultivations = plan.Group!.Plots.SelectMany(pl => pl.PlotCultivations).ToList();
            if (!plotCultivations.Any())
            {
                _logger.LogWarning("No plot cultivations found for plan {PlanId}", notification.PlanId);
                return;
            }
            
            //Populate active version lookup for each PlotCultivation
            var plotCultivationIds = plotCultivations.Select(pc => pc.Id).ToList();
            var activeVersions = await _versionRepo.ListAsync(
                filter: v => plotCultivationIds.Contains(v.PlotCultivationId) && v.IsActive
            );
            var versionLookup = activeVersions.ToDictionary(v => v.PlotCultivationId, v => v.Id);
            
            //Begin build
            var cultivationTasks = new List<CultivationTask>();
            var cultivationTaskMaterials = new List<CultivationTaskMaterial>();

            foreach (var stage in plan.CurrentProductionStages)
            {
                foreach (var planTask in stage.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
                {
                    foreach (var plotCultivation in plotCultivations)
                    {
                        if (plotCultivation.Area <= 0)
                        {
                            _logger.LogWarning("Skipping zero-area plot {PlotId} for task {TaskId}", plotCultivation.Id, planTask.Id);
                            continue;
                        }

                        // Get the active version for this PlotCultivation
                        versionLookup.TryGetValue(plotCultivation.Id, out var versionId);
                        
                        if (versionId == Guid.Empty)
                        {
                            _logger.LogWarning("No active version found for PlotCultivation {PlotCultivationId}", plotCultivation.Id);
                        }

                        var (task, taskMaterials) = CreateTaskForPlot(planTask, plotCultivation, versionId, priceDict, materialDict);
                        cultivationTasks.Add(task);
                        cultivationTaskMaterials.AddRange(taskMaterials);
                    }
                }
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
                      .ThenInclude(g => g.Plots)
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
            //After order price by older, get the first for the current available prices
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
            Status = TaskStatus.InProgress,
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

