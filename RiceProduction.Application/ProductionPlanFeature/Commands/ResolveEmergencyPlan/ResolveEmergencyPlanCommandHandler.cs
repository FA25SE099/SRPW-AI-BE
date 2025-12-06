using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            // 1. Get the production plan with stages
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

            // 2. Validate plan status
            if (plan.Status != RiceProduction.Domain.Enums.TaskStatus.Emergency)
            {
                return Result<Guid>.Failure(
                    $"Plan is currently in status '{plan.Status}'. Only Emergency plans can be resolved.",
                    "InvalidStatus");
            }

            // 3. Find or create emergency ProductionStage
            ProductionStage productionStage;

            if (!string.IsNullOrEmpty(request.ProductionStageId.ToString()))
            {
                // Use specified stage
                productionStage = plan.CurrentProductionStages
                    .FirstOrDefault(s => s.Id == request.ProductionStageId);

                if (productionStage == null)
                {
                    return Result<Guid>.Failure(
                        $"Production Stage with ID {request.ProductionStageId} not found in this plan.",
                        "ProductionStageNotFound");
                }
            }
            else
            {
                // Find existing emergency stage
                productionStage = plan.CurrentProductionStages
                    .FirstOrDefault(s => s.StageName == "Emergency Response" ||
                                        s.StageName.Contains("Emergency"));

                if (productionStage == null)
                {
                    // Create new emergency stage
                    productionStage = new ProductionStage
                    {
                        ProductionPlanId = plan.Id,
                        StageName = "Emergency Response",
                        Description = "Emergency treatment stage for urgent interventions",
                        SequenceOrder = plan.CurrentProductionStages.Any()
                            ? plan.CurrentProductionStages.Max(s => s.SequenceOrder) + 1
                            : 1,
                        TypicalDurationDays = 7
                    };

                    await _unitOfWork.Repository<ProductionStage>().AddAsync(productionStage);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Created new emergency ProductionStage '{StageName}' with ID {StageId} for Plan {PlanId}",
                        productionStage.StageName, productionStage.Id, plan.Id);
                }
            }

            // 4. Validate all materials exist
            var allMaterialIds = request.BaseCultivationTasks
                .SelectMany(t => t.MaterialsPerHectare)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            if (allMaterialIds.Any())
            {
                var existingMaterialIds = await _unitOfWork.Repository<Material>()
                    .GetQueryable()
                    .Where(m => allMaterialIds.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync(cancellationToken);

                var missingMaterialIds = allMaterialIds.Except(existingMaterialIds).ToList();
                if (missingMaterialIds.Any())
                {
                    return Result<Guid>.Failure(
                        $"The following materials were not found: {string.Join(", ", missingMaterialIds)}",
                        "MaterialsNotFound");
                }
            }

            // 5. Validate existing ProductionPlanTasks (if specified)
            var existingTaskIds = request.BaseCultivationTasks
                .Where(t => t.ProductionPlanTaskId.HasValue)
                .Select(t => t.ProductionPlanTaskId!.Value)
                .Distinct()
                .ToList();

            if (existingTaskIds.Any())
            {
                var validTaskIds = await _unitOfWork.Repository<ProductionPlanTask>()
                    .GetQueryable()
                    .Where(t => existingTaskIds.Contains(t.Id))
                    .Select(t => t.Id)
                    .ToListAsync(cancellationToken);

                var invalidTaskIds = existingTaskIds.Except(validTaskIds).ToList();
                if (invalidTaskIds.Any())
                {
                    return Result<Guid>.Failure(
                        $"The following production plan tasks were not found: {string.Join(", ", invalidTaskIds)}",
                        "ProductionPlanTasksNotFound");
                }
            }

            // 6. Get all plots and validate
            var plots = await _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Include(p => p.PlotCultivations)
                .Where(p => request.PlotIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            if (plots.Count != request.PlotIds.Count)
            {
                var foundPlotIds = plots.Select(p => p.Id).ToList();
                var missingPlotIds = request.PlotIds.Except(foundPlotIds).ToList();
                return Result<Guid>.Failure(
                    $"The following plots were not found: {string.Join(", ", missingPlotIds)}",
                    "PlotsNotFound");
            }

            // 7. Load the production plan's group and validate plots
            var planWithGroup = await _unitOfWork.Repository<ProductionPlan>()
                .GetQueryable()
                .Include(p => p.Group)
                    .ThenInclude(g => g!.GroupPlots)
                        .ThenInclude(gp => gp.Plot)
                            .ThenInclude(p => p.PlotCultivations)
                .FirstOrDefaultAsync(p => p.Id == plan.Id, cancellationToken);

            if (planWithGroup?.Group == null)
            {
                return Result<Guid>.Failure(
                    "Production plan does not have an associated group.",
                    "GroupNotFound");
            }

            var groupPlotIds = planWithGroup.Group.GroupPlots.Select(gp => gp.PlotId).ToHashSet();

            // Validate that all requested plots are in the group
            var invalidPlotIds = request.PlotIds.Except(groupPlotIds).ToList();
            if (invalidPlotIds.Any())
            {
                var invalidPlotDetails = plots
                    .Where(p => invalidPlotIds.Contains(p.Id))
                    .Select(p => $"{p.Id} (SoThua: {p.SoThua}, SoTo: {p.SoTo})")
                    .ToList();

                return Result<Guid>.Failure(
                    $"The following plots are not in the production plan's group: {string.Join(", ", invalidPlotDetails)}",
                    "PlotNotInPlan");
            }

            // 7.5. Map plots to their PlotCultivations (with auto-creation)
            var plotCultivationMap = new Dictionary<Guid, PlotCultivation>();

            foreach (var plot in plots)
            {
                // Find the PlotCultivation that matches the group's season, variety, and planting date
                var plotCultivation = plot.PlotCultivations
                    .FirstOrDefault(pc =>
                        pc.SeasonId == planWithGroup.Group.SeasonId &&
                        pc.RiceVarietyId == planWithGroup.Group.RiceVarietyId &&
                        pc.PlantingDate.Date == planWithGroup.Group.PlantingDate!.Value.Date);

                if (plotCultivation == null)
                {
                    // AUTO-CREATE: Create a new PlotCultivation for this plot
                    _logger.LogWarning(
                        "Plot {PlotId} (SoThua: {SoThua}, SoTo: {SoTo}) does not have a PlotCultivation. Creating one automatically for Season {SeasonId}, Variety {VarietyId}, PlantingDate {PlantingDate}.",
                        plot.Id, plot.SoThua, plot.SoTo,
                        planWithGroup.Group.SeasonId,
                        planWithGroup.Group.RiceVarietyId,
                        planWithGroup.Group.PlantingDate);

                    plotCultivation = new PlotCultivation
                    {
                        PlotId = plot.Id,
                        SeasonId = planWithGroup.Group.SeasonId!.Value,
                        RiceVarietyId = planWithGroup.Group.RiceVarietyId!.Value,
                        PlantingDate = planWithGroup.Group.PlantingDate!.Value,
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
                plots.Count, plan.Id, planWithGroup.Group.Id);

            // 8. Check for duplicate version name in any PlotCultivation
            var plotCultivationIds = plotCultivationMap.Values.Select(pc => pc.Id).ToList();
            var existingVersions = await _unitOfWork.Repository<CultivationVersion>()
                .ListAsync(v => plotCultivationIds.Contains(v.PlotCultivationId));

            var duplicateVersionExists = existingVersions
                .Any(v => v.VersionName.ToLower() == request.NewVersionName.ToLower());

            if (duplicateVersionExists)
            {
                return Result<Guid>.Failure(
                    $"A version with the name '{request.NewVersionName}' already exists for one or more plot cultivations.",
                    "DuplicateVersionName");
            }

            // 9. Process each base cultivation task (create ProductionPlanTasks)
            var productionPlanTaskMap = new Dictionary<int, ProductionPlanTask>();
            var taskIndex = 0;

            foreach (var baseTask in request.BaseCultivationTasks)
            {
                ProductionPlanTask productionPlanTask;

                if (baseTask.ProductionPlanTaskId.HasValue)
                {
                    // Use existing ProductionPlanTask
                    productionPlanTask = await _unitOfWork.Repository<ProductionPlanTask>()
                        .GetQueryable()
                        .Include(t => t.ProductionPlanTaskMaterials)
                        .FirstOrDefaultAsync(t => t.Id == baseTask.ProductionPlanTaskId.Value, cancellationToken);

                    if (productionPlanTask == null)
                    {
                        return Result<Guid>.Failure(
                            $"ProductionPlanTask with ID {baseTask.ProductionPlanTaskId} not found.",
                            "ProductionPlanTaskNotFound");
                    }
                }
                else
                {
                    // Create new emergency ProductionPlanTask
                    productionPlanTask = new ProductionPlanTask
                    {
                        ProductionStageId = productionStage.Id,
                        TaskName = baseTask.TaskName ?? "EmergencySolution",
                        Description = baseTask.Description ?? "Emergency pest control solution",
                        TaskType = baseTask.TaskType ?? RiceProduction.Domain.Enums.TaskType.PestControl,
                        ScheduledDate = DateTime.UtcNow,
                        Status = baseTask.Status ?? RiceProduction.Domain.Enums.TaskStatus.Draft,
                        Priority = TaskPriority.High,
                        SequenceOrder = productionStage.ProductionPlanTasks.Count + taskIndex + 1,
                        EstimatedMaterialCost = 0
                    };

                    await _unitOfWork.Repository<ProductionPlanTask>().AddAsync(productionPlanTask);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Add materials to ProductionPlanTask
                    foreach (var material in baseTask.MaterialsPerHectare)
                    {
                        var taskMaterial = new ProductionPlanTaskMaterial
                        {
                            ProductionPlanTaskId = productionPlanTask.Id,
                            MaterialId = material.MaterialId,
                            QuantityPerHa = material.QuantityPerHa
                        };

                        await _unitOfWork.Repository<ProductionPlanTaskMaterial>().AddAsync(taskMaterial);
                    }

                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Created new ProductionPlanTask '{TaskName}' with ID {TaskId} and {MaterialCount} materials",
                        productionPlanTask.TaskName, productionPlanTask.Id, baseTask.MaterialsPerHectare.Count);
                }

                productionPlanTaskMap[taskIndex] = productionPlanTask;
                taskIndex++;
            }

            // 10. Create CultivationTasks for each plot × each base task (WITHOUT version assignment yet)
            var cultivationTasks = new List<CultivationTask>();
            var totalArea = plots.Sum(p => p.Area);

            foreach (var plot in plots)
            {
                var plotCultivation = plotCultivationMap[plot.Id];
                var plotArea = plot.Area;

                // Create cultivation task for each base task template
                for (int i = 0; i < request.BaseCultivationTasks.Count; i++)
                {
                    var baseTask = request.BaseCultivationTasks[i];
                    var productionPlanTask = productionPlanTaskMap[i];

                    var cultivationTask = new CultivationTask
                    {
                        ProductionPlanTaskId = productionPlanTask.Id,
                        PlotCultivationId = plotCultivation.Id,
                        // ✅ Version will be assigned AFTER creation
                        AssignedToUserId = baseTask.DefaultAssignedToUserId,
                        AssignedToVendorId = baseTask.DefaultAssignedToVendorId,
                        CultivationTaskName = $"{baseTask.TaskName ?? productionPlanTask.TaskName} - Plot {plot.SoThua}/{plot.SoTo}",
                        Description = $"{baseTask.Description ?? productionPlanTask.Description} | Area: {plotArea} ha",
                        TaskType = baseTask.TaskType ?? productionPlanTask.TaskType,
                        ScheduledEndDate = baseTask.ScheduledEndDate ?? DateTime.UtcNow.AddDays(7),
                        Status = baseTask.Status ?? RiceProduction.Domain.Enums.TaskStatus.Draft,
                        ExecutionOrder = baseTask.ExecutionOrder,
                        IsContingency = baseTask.IsContingency,
                        ContingencyReason = baseTask.ContingencyReason ?? request.ResolutionReason ?? "Emergency resolution"
                    };

                    // Add materials scaled by plot area
                    foreach (var materialRequest in baseTask.MaterialsPerHectare)
                    {
                        var scaledQuantity = materialRequest.QuantityPerHa * plotArea;

                        var taskMaterial = new CultivationTaskMaterial
                        {
                            MaterialId = materialRequest.MaterialId,
                            ActualQuantity = scaledQuantity,
                            ActualCost = 0,
                            Notes = materialRequest.Notes ?? $"Scaled from {materialRequest.QuantityPerHa}/ha × {plotArea} ha"
                        };

                        cultivationTask.CultivationTaskMaterials.Add(taskMaterial);
                    }

                    cultivationTasks.Add(cultivationTask);
                }

                _logger.LogInformation(
                    "Created {TaskCount} cultivation tasks for Plot {PlotId} ({Area} ha)",
                    request.BaseCultivationTasks.Count, plot.Id, plotArea);
            }

            // 11. Add all cultivation tasks to database
            foreach (var task in cultivationTasks)
            {
                await _unitOfWork.Repository<CultivationTask>().AddAsync(task);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully created {TotalTaskCount} cultivation tasks ({TaskTypeCount} types × {PlotCount} plots). Total area: {TotalArea} ha",
                cultivationTasks.Count, request.BaseCultivationTasks.Count, plots.Count, totalArea);

            // ✅ 12. NOW create cultivation versions (after all tasks are successfully created)
            // Create a new version for each PlotCultivation
            var newVersions = new List<CultivationVersion>();
            var plotCultivationVersionMap = new Dictionary<Guid, Guid>();

            foreach (var plotCultivation in plotCultivationMap.Values)
            {
                // Deactivate all previous versions for this PlotCultivation
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

                // Calculate next version order for this PlotCultivation
                var maxVersionOrder = existingVersionsForPlot.Any()
                    ? existingVersionsForPlot.Max(v => v.VersionOrder)
                    : 0;

                // Create new cultivation version for this PlotCultivation
                var newVersion = new CultivationVersion
                {
                    PlotCultivationId = plotCultivation.Id,
                    VersionName = request.NewVersionName.Trim(),
                    VersionOrder = maxVersionOrder + 1,
                    IsActive = true,
                    Reason = request.ResolutionReason?.Trim() ?? "Emergency resolution",
                    ActivatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Repository<CultivationVersion>().AddAsync(newVersion);
                newVersions.Add(newVersion);
                plotCultivationVersionMap[plotCultivation.Id] = newVersion.Id;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created {VersionCount} new CultivationVersions '{VersionName}' for PlotCultivations in Plan {PlanId}",
                newVersions.Count, request.NewVersionName, plan.Id);

            // ✅ 13. Assign version ID to all created cultivation tasks
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

            // 14. Change plan status back to Approved
            plan.Status = RiceProduction.Domain.Enums.TaskStatus.Approved;
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
}