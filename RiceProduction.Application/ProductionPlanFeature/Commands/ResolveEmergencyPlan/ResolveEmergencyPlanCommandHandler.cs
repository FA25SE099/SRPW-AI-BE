using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Commands.ResolveEmergencyPlan;

public class ResolveEmergencyPlanCommandHandler : IRequestHandler<ResolveEmergencyPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResolveEmergencyPlanCommandHandler> _logger;
    private readonly IMediator _mediator;

    public ResolveEmergencyPlanCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ResolveEmergencyPlanCommandHandler> logger,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result<Guid>> Handle(ResolveEmergencyPlanCommand request, CancellationToken cancellationToken)
    {
        var expertId = request.ExpertId;
        if (!expertId.HasValue)
        {
            return Result<Guid>.Failure("Current expert user ID not found.", "AuthenticationRequired");
        }

        try
        {
            var plan = await _unitOfWork.Repository<ProductionPlan>()
                .GetQueryable()
                .Include(p => p.CurrentProductionStages)
                    .ThenInclude(s => s.ProductionPlanTasks)
                        .ThenInclude(t => t.ProductionPlanTaskMaterials)
                .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

            if (plan == null)
            {
                return Result<Guid>.Failure(
                    $"Production Plan with ID {request.PlanId} not found.",
                    "PlanNotFound");
            }

            if (plan.Status != TaskStatus.Emergency)
            {
                return Result<Guid>.Failure(
                    $"Plan is currently in status '{plan.Status}'. Only Emergency plans can be resolved.",
                    "InvalidStatus");
            }

            var stageResult = await ResolveOrCreateProductionStageAsync(plan, request.ProductionStageId, cancellationToken);
            if (!stageResult.Succeeded)
            {
                return Result<Guid>.Failure(stageResult.Errors, stageResult.Message);
            }

            var productionStage = stageResult.Data!;

            var materialValidation = await ValidateMaterialsAsync(request.BaseCultivationTasks, cancellationToken);
            if (materialValidation != null)
            {
                return materialValidation;
            }

            var taskValidation = await ValidateExistingPlanTasksAsync(request.BaseCultivationTasks, cancellationToken);
            if (taskValidation != null)
            {
                return taskValidation;
            }

            var plotMappingResult = await ValidateAndMapPlotsAsync(plan, request.PlotIds, cancellationToken);
            if (!plotMappingResult.Succeeded)
            {
                return Result<Guid>.Failure(plotMappingResult.Errors, plotMappingResult.Message);
            }

            var (plots, plotCultivationMap) = plotMappingResult.Data;

            var plotCultivationIds = plotCultivationMap.Values.Select(pc => pc.Id).ToList();
            var existingVersions = await _unitOfWork.Repository<CultivationVersion>()
                .ListAsync(v => plotCultivationIds.Contains(v.PlotCultivationId));

            if (existingVersions.Any(v => v.VersionName.Equals(request.NewVersionName, StringComparison.OrdinalIgnoreCase)))
            {
                return Result<Guid>.Failure(
                    $"A version with the name '{request.NewVersionName}' already exists for one or more plot cultivations.",
                    "DuplicateVersionName");
            }

            var planTasksResult = await ResolveOrCreatePlanTasksAsync(
                request.BaseCultivationTasks, productionStage, cancellationToken);
            if (!planTasksResult.Succeeded)
            {
                return Result<Guid>.Failure(planTasksResult.Errors, planTasksResult.Message);
            }

            var totalArea = plots.Sum(p => p.Area);
            var cultivationTasks = BuildCultivationTasks(plots, plotCultivationMap, request, planTasksResult.Data!);

            await _unitOfWork.Repository<CultivationTask>().AddRangeAsync(cultivationTasks);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully created {TotalTaskCount} cultivation tasks ({TaskTypeCount} types × {PlotCount} plots). Total area: {TotalArea} ha",
                cultivationTasks.Count, request.BaseCultivationTasks.Count, plots.Count, totalArea);

            // Create cultivation versions after tasks are persisted, then link tasks to their new versions
            var (newVersions, plotCultivationVersionMap) = await CreateAndActivateVersionsAsync(
                plotCultivationMap.Values, request.NewVersionName, request.ResolutionReason, plan.Id, cancellationToken);

            foreach (var task in cultivationTasks)
            {
                if (plotCultivationVersionMap.TryGetValue(task.PlotCultivationId, out var versionId))
                {
                    task.VersionId = versionId;
                }
            }

            _unitOfWork.Repository<CultivationTask>().UpdateRange(cultivationTasks);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Assigned VersionIds to {TaskCount} cultivation tasks across {VersionCount} versions",
                cultivationTasks.Count, newVersions.Count);

            plan.Status = TaskStatus.Approved;
            plan.LastModified = DateTime.UtcNow;
            plan.LastModifiedBy = expertId;

            _unitOfWork.Repository<ProductionPlan>().Update(plan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully resolved emergency for Plan {PlanId}. Status changed to Approved. Created {VersionCount} versions '{VersionName}' with {TaskCount} cultivation tasks across {PlotCount} plots by Expert {ExpertId}",
                plan.Id, newVersions.Count, request.NewVersionName, cultivationTasks.Count, plots.Count, expertId);

            return Result<Guid>.Success(
                plan.Id,
                $"Emergency plan resolved successfully. Created {newVersions.Count} versions '{request.NewVersionName}' with {cultivationTasks.Count} cultivation tasks ({request.BaseCultivationTasks.Count} task types × {plots.Count} plots). Total area: {totalArea:F2} ha. Plan status changed to Approved.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while resolving emergency plan {PlanId}",
                request.PlanId);

            return Result<Guid>.Failure(
                "A database error occurred while resolving the emergency plan.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while resolving emergency plan {PlanId}",
                request.PlanId);

            return Result<Guid>.Failure(
                "An unexpected error occurred while resolving the emergency plan.",
                "ResolveEmergencyPlanFailed");
        }
    }

    private async Task<Result<ProductionStage>> ResolveOrCreateProductionStageAsync(
        ProductionPlan plan,
        Guid productionStageId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(productionStageId.ToString()))
        {
            var specifiedStage = plan.CurrentProductionStages
                .FirstOrDefault(s => s.Id == productionStageId);

            return specifiedStage != null
                ? Result<ProductionStage>.Success(specifiedStage)
                : Result<ProductionStage>.Failure(
                    $"Production Stage with ID {productionStageId} not found in this plan.",
                    "ProductionStageNotFound");
        }

        var existingEmergencyStage = plan.CurrentProductionStages
            .FirstOrDefault(s => s.StageName == "Emergency Response" || s.StageName.Contains("Emergency"));

        if (existingEmergencyStage != null)
        {
            return Result<ProductionStage>.Success(existingEmergencyStage);
        }

        var newStage = new ProductionStage
        {
            ProductionPlanId = plan.Id,
            StageName = "Emergency Response",
            Description = "Emergency treatment stage for urgent interventions",
            SequenceOrder = plan.CurrentProductionStages.Any()
                ? plan.CurrentProductionStages.Max(s => s.SequenceOrder) + 1
                : 1,
            TypicalDurationDays = 7
        };

        await _unitOfWork.Repository<ProductionStage>().AddAsync(newStage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created new emergency ProductionStage '{StageName}' with ID {StageId} for Plan {PlanId}",
            newStage.StageName, newStage.Id, plan.Id);

        return Result<ProductionStage>.Success(newStage);
    }

    private async Task<Result<Guid>?> ValidateMaterialsAsync(
        List<BaseCultivationTaskRequest> baseTasks,
        CancellationToken cancellationToken)
    {
        var allMaterialIds = baseTasks
            .SelectMany(t => t.MaterialsPerHectare)
            .Select(m => m.MaterialId)
            .Distinct()
            .ToList();

        if (!allMaterialIds.Any())
        {
            return null;
        }

        var existingMaterialIds = await _unitOfWork.Repository<Material>()
            .GetQueryable()
            .Where(m => allMaterialIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var missingMaterialIds = allMaterialIds.Except(existingMaterialIds).ToList();
        return missingMaterialIds.Any()
            ? Result<Guid>.Failure(
                $"The following materials were not found: {string.Join(", ", missingMaterialIds)}",
                "MaterialsNotFound")
            : null;
    }

    private async Task<Result<Guid>?> ValidateExistingPlanTasksAsync(
        List<BaseCultivationTaskRequest> baseTasks,
        CancellationToken cancellationToken)
    {
        var existingTaskIds = baseTasks
            .Where(t => t.ProductionPlanTaskId.HasValue)
            .Select(t => t.ProductionPlanTaskId!.Value)
            .Distinct()
            .ToList();

        if (!existingTaskIds.Any())
        {
            return null;
        }

        var validTaskIds = await _unitOfWork.Repository<ProductionPlanTask>()
            .GetQueryable()
            .Where(t => existingTaskIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var invalidTaskIds = existingTaskIds.Except(validTaskIds).ToList();
        return invalidTaskIds.Any()
            ? Result<Guid>.Failure(
                $"The following production plan tasks were not found: {string.Join(", ", invalidTaskIds)}",
                "ProductionPlanTasksNotFound")
            : null;
    }

    private async Task<Result<(List<Plot> Plots, Dictionary<Guid, PlotCultivation> PlotCultivationMap)>> ValidateAndMapPlotsAsync(
        ProductionPlan plan,
        List<Guid> requestedPlotIds,
        CancellationToken cancellationToken)
    {
        var plots = await _unitOfWork.Repository<Plot>()
            .GetQueryable()
            .Include(p => p.PlotCultivations)
            .Where(p => requestedPlotIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (plots.Count != requestedPlotIds.Count)
        {
            var foundPlotIds = plots.Select(p => p.Id).ToList();
            var missingPlotIds = requestedPlotIds.Except(foundPlotIds).ToList();
            return Result<(List<Plot>, Dictionary<Guid, PlotCultivation>)>.Failure(
                $"The following plots were not found: {string.Join(", ", missingPlotIds)}",
                "PlotsNotFound");
        }

        var planWithGroup = await _unitOfWork.Repository<ProductionPlan>()
            .GetQueryable()
            .Include(p => p.Group)
                .ThenInclude(g => g!.YearSeason)
            .Include(p => p.Group)
                .ThenInclude(g => g!.GroupPlots)
                    .ThenInclude(gp => gp.Plot)
                        .ThenInclude(p => p.PlotCultivations)
            .FirstOrDefaultAsync(p => p.Id == plan.Id, cancellationToken);

        if (planWithGroup?.Group == null)
        {
            return Result<(List<Plot>, Dictionary<Guid, PlotCultivation>)>.Failure(
                "Production plan does not have an associated group.",
                "GroupNotFound");
        }

        var group = planWithGroup.Group;
        var groupPlotIds = group.GroupPlots.Select(gp => gp.PlotId).ToHashSet();

        var invalidPlotIds = requestedPlotIds.Except(groupPlotIds).ToList();
        if (invalidPlotIds.Any())
        {
            var invalidPlotDetails = plots
                .Where(p => invalidPlotIds.Contains(p.Id))
                .Select(p => $"{p.Id} (SoThua: {p.SoThua}, SoTo: {p.SoTo})")
                .ToList();

            return Result<(List<Plot>, Dictionary<Guid, PlotCultivation>)>.Failure(
                $"The following plots are not in the production plan's group: {string.Join(", ", invalidPlotDetails)}",
                "PlotNotInPlan");
        }

        var plotCultivationMap = new Dictionary<Guid, PlotCultivation>();
        var seasonId = group.YearSeason!.SeasonId;
        var riceVarietyId = group.YearSeason!.RiceVarietyId;
        var plantingDate = group.PlantingDate!.Value;

        foreach (var plot in plots)
        {
            var plotCultivation = plot.PlotCultivations
                .FirstOrDefault(pc =>
                    pc.SeasonId == seasonId &&
                    pc.RiceVarietyId == riceVarietyId &&
                    pc.PlantingDate.Date == plantingDate.Date);

            if (plotCultivation == null)
            {
                _logger.LogWarning(
                    "Plot {PlotId} (SoThua: {SoThua}, SoTo: {SoTo}) does not have a PlotCultivation. Creating one automatically for Season {SeasonId}, Variety {VarietyId}, PlantingDate {PlantingDate}.",
                    plot.Id, plot.SoThua, plot.SoTo, seasonId, riceVarietyId ?? Guid.Empty, group.PlantingDate);

                plotCultivation = new PlotCultivation
                {
                    PlotId = plot.Id,
                    SeasonId = seasonId,
                    RiceVarietyId = riceVarietyId ?? Guid.Empty,
                    PlantingDate = plantingDate,
                    Area = plot.Area,
                    Status = CultivationStatus.Planned,
                    ExpectedYield = plot.Area * 6.0m // Default: 6 tons/ha
                };

                await _unitOfWork.Repository<PlotCultivation>().AddAsync(plotCultivation);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Created PlotCultivation {PlotCultivationId} for Plot {PlotId} (SoThua: {SoThua}, SoTo: {SoTo})",
                    plotCultivation.Id, plot.Id, plot.SoThua, plot.SoTo);
            }

            plotCultivationMap[plot.Id] = plotCultivation;
        }

        _logger.LogInformation(
            "Validated and mapped {PlotCount} plots for Plan {PlanId} in Group {GroupId}",
            plots.Count, plan.Id, group.Id);

        return Result<(List<Plot>, Dictionary<Guid, PlotCultivation>)>.Success((plots, plotCultivationMap));
    }

    private async Task<Result<Dictionary<int, ProductionPlanTask>>> ResolveOrCreatePlanTasksAsync(
        List<BaseCultivationTaskRequest> baseTasks,
        ProductionStage productionStage,
        CancellationToken cancellationToken)
    {
        var productionPlanTaskMap = new Dictionary<int, ProductionPlanTask>();

        for (int taskIndex = 0; taskIndex < baseTasks.Count; taskIndex++)
        {
            var baseTask = baseTasks[taskIndex];
            ProductionPlanTask? productionPlanTask;

            if (baseTask.ProductionPlanTaskId.HasValue)
            {
                productionPlanTask = await _unitOfWork.Repository<ProductionPlanTask>()
                    .GetQueryable()
                    .Include(t => t.ProductionPlanTaskMaterials)
                    .FirstOrDefaultAsync(t => t.Id == baseTask.ProductionPlanTaskId.Value, cancellationToken);

                if (productionPlanTask == null)
                {
                    return Result<Dictionary<int, ProductionPlanTask>>.Failure(
                        $"ProductionPlanTask with ID {baseTask.ProductionPlanTaskId} not found.",
                        "ProductionPlanTaskNotFound");
                }
            }
            else
            {
                productionPlanTask = new ProductionPlanTask
                {
                    ProductionStageId = productionStage.Id,
                    TaskName = baseTask.TaskName ?? "EmergencySolution",
                    Description = baseTask.Description ?? "Emergency pest control solution",
                    TaskType = baseTask.TaskType ?? TaskType.PestControl,
                    ScheduledDate = DateTime.UtcNow,
                    Status = baseTask.Status ?? TaskStatus.Draft,
                    Priority = TaskPriority.High,
                    SequenceOrder = productionStage.ProductionPlanTasks.Count + taskIndex + 1,
                    EstimatedMaterialCost = 0
                };

                await _unitOfWork.Repository<ProductionPlanTask>().AddAsync(productionPlanTask);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                foreach (var material in baseTask.MaterialsPerHectare)
                {
                    await _unitOfWork.Repository<ProductionPlanTaskMaterial>().AddAsync(new ProductionPlanTaskMaterial
                    {
                        ProductionPlanTaskId = productionPlanTask.Id,
                        MaterialId = material.MaterialId,
                        QuantityPerHa = material.QuantityPerHa
                    });
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Created new ProductionPlanTask '{TaskName}' with ID {TaskId} and {MaterialCount} materials",
                    productionPlanTask.TaskName, productionPlanTask.Id, baseTask.MaterialsPerHectare.Count);
            }

            productionPlanTaskMap[taskIndex] = productionPlanTask;
        }

        return Result<Dictionary<int, ProductionPlanTask>>.Success(productionPlanTaskMap);
    }

    private List<CultivationTask> BuildCultivationTasks(
        List<Plot> plots,
        Dictionary<Guid, PlotCultivation> plotCultivationMap,
        ResolveEmergencyPlanCommand request,
        Dictionary<int, ProductionPlanTask> productionPlanTaskMap)
    {
        var cultivationTasks = new List<CultivationTask>();

        foreach (var plot in plots)
        {
            var plotCultivation = plotCultivationMap[plot.Id];
            var plotArea = plot.Area;

            for (int i = 0; i < request.BaseCultivationTasks.Count; i++)
            {
                var baseTask = request.BaseCultivationTasks[i];
                var productionPlanTask = productionPlanTaskMap[i];

                var cultivationTask = new CultivationTask
                {
                    ProductionPlanTaskId = productionPlanTask.Id,
                    PlotCultivationId = plotCultivation.Id,
                    AssignedToUserId = baseTask.DefaultAssignedToUserId,
                    AssignedToVendorId = baseTask.DefaultAssignedToVendorId,
                    CultivationTaskName = $"{baseTask.TaskName ?? productionPlanTask.TaskName} - Plot {plot.SoThua}/{plot.SoTo}",
                    Description = $"{baseTask.Description ?? productionPlanTask.Description} | Area: {plotArea} ha",
                    TaskType = baseTask.TaskType ?? productionPlanTask.TaskType,
                    ScheduledEndDate = baseTask.ScheduledEndDate ?? DateTime.UtcNow.AddDays(7),
                    Status = baseTask.Status ?? TaskStatus.Draft,
                    ExecutionOrder = baseTask.ExecutionOrder,
                    IsContingency = baseTask.IsContingency,
                    ContingencyReason = baseTask.ContingencyReason ?? request.ResolutionReason ?? "Emergency resolution"
                };

                foreach (var materialRequest in baseTask.MaterialsPerHectare)
                {
                    cultivationTask.CultivationTaskMaterials.Add(new CultivationTaskMaterial
                    {
                        MaterialId = materialRequest.MaterialId,
                        ActualQuantity = materialRequest.QuantityPerHa * plotArea,
                        ActualCost = 0,
                        Notes = materialRequest.Notes ?? $"Scaled from {materialRequest.QuantityPerHa}/ha × {plotArea} ha"
                    });
                }

                cultivationTasks.Add(cultivationTask);
            }

            _logger.LogInformation(
                "Created {TaskCount} cultivation tasks for Plot {PlotId} ({Area} ha)",
                request.BaseCultivationTasks.Count, plot.Id, plotArea);
        }

        return cultivationTasks;
    }

    private async Task<(List<CultivationVersion> NewVersions, Dictionary<Guid, Guid> VersionMap)> CreateAndActivateVersionsAsync(
        ICollection<PlotCultivation> plotCultivations,
        string newVersionName,
        string? resolutionReason,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var newVersions = new List<CultivationVersion>();
        var plotCultivationVersionMap = new Dictionary<Guid, Guid>();

        foreach (var plotCultivation in plotCultivations)
        {
            var existingVersionsForPlot = await _unitOfWork.Repository<CultivationVersion>()
                .ListAsync(v => v.PlotCultivationId == plotCultivation.Id);

            foreach (var existingVersion in existingVersionsForPlot)
            {
                existingVersion.IsActive = false;
            }

            if (existingVersionsForPlot.Any())
            {
                _unitOfWork.Repository<CultivationVersion>().UpdateRange(existingVersionsForPlot);
            }

            var maxVersionOrder = existingVersionsForPlot.Any()
                ? existingVersionsForPlot.Max(v => v.VersionOrder)
                : 0;

            var newVersion = new CultivationVersion
            {
                PlotCultivationId = plotCultivation.Id,
                VersionName = newVersionName.Trim(),
                VersionOrder = maxVersionOrder + 1,
                IsActive = true,
                Reason = resolutionReason?.Trim() ?? "Emergency resolution",
                ActivatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<CultivationVersion>().AddAsync(newVersion);
            newVersions.Add(newVersion);
            plotCultivationVersionMap[plotCultivation.Id] = newVersion.Id;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created {VersionCount} new CultivationVersions '{VersionName}' for PlotCultivations in Plan {PlanId}",
            newVersions.Count, newVersionName, planId);

        return (newVersions, plotCultivationVersionMap);
    }
}