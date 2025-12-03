using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ReportFeature.Command;

public class CreateEmergencyPlanForPlotCommandHandler : IRequestHandler<CreateEmergencyPlanForPlotCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateEmergencyPlanForPlotCommandHandler> _logger;
    private readonly IMediator _mediator;

    public CreateEmergencyPlanForPlotCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateEmergencyPlanForPlotCommandHandler> logger,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result<Guid>> Handle(CreateEmergencyPlanForPlotCommand request, CancellationToken cancellationToken)
    {
        var expertId = request.ExpertId;
        if (!expertId.HasValue)
        {
            return Result<Guid>.Failure("Current expert user ID not found.", "AuthenticationRequired");
        }

        try
        {
            // 1. Get Emergency Report with PlotCultivation → Plot
            var emergencyReport = await _unitOfWork.Repository<EmergencyReport>()
                .GetQueryable()
                .Include(er => er.PlotCultivation)
                    .ThenInclude(pc => pc!.Plot)
                .FirstOrDefaultAsync(er => er.Id == request.EmergencyReportId, cancellationToken);

            if (emergencyReport == null)
            {
                return Result<Guid>.Failure(
                    $"Emergency Report with ID {request.EmergencyReportId} not found.",
                    "EmergencyReportNotFound");
            }

            var plot = emergencyReport.PlotCultivation?.Plot;
            if (plot == null)
            {
                return Result<Guid>.Failure("Emergency report is not linked to a valid plot.", "PlotNotFound");
            }

            var plotCultivation = emergencyReport.PlotCultivation;
            if (plotCultivation == null)
            {
                return Result<Guid>.Failure("PlotCultivation not found for this emergency report.", "PlotCultivationNotFound");
            }

            // 2. Validate Production Plan exists (optional context, but good to check)
            var plan = await _unitOfWork.Repository<ProductionPlan>()
                .GetQueryable()
                .Include(p => p.Group)
                .FirstOrDefaultAsync(p => p.Id == request.ProductionPlanId, cancellationToken);

            if (plan == null)
            {
                return Result<Guid>.Failure(
                    $"Production Plan with ID {request.ProductionPlanId} not found.",
                    "PlanNotFound");
            }

            // Optional: validate plot belongs to the plan's group
            if (plan.Group == null || !plan.Group.Plots.Any(p => p.Id == plot.Id))
            {
                _logger.LogWarning("Plot {PlotId} does not belong to Production Plan {PlanId} group, but continuing for emergency.", plot.Id, plan.Id);
            }

            // 3. Validate all requested materials exist
            var requestedMaterialIds = request.EmergencyTasks
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            if (requestedMaterialIds.Any())
            {
                var existingMaterialIds = await _unitOfWork.Repository<Material>()
                    .GetQueryable()
                    .Where(m => requestedMaterialIds.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync(cancellationToken);

                var missing = requestedMaterialIds.Except(existingMaterialIds).ToList();
                if (missing.Any())
                {
                    return Result<Guid>.Failure(
                        $"Materials not found: {string.Join(", ", missing)}",
                        "MaterialsNotFound");
                }
            }

            // 4. Check for duplicate version name
            var existingVersions = await _unitOfWork.Repository<CultivationVersion>()
                .ListAsync(v => v.PlotCultivationId == plotCultivation.Id);

            if (existingVersions.Any(v =>
                v.VersionName.Equals(request.NewVersionName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Result<Guid>.Failure(
                    $"Version name '{request.NewVersionName}' already exists for this plot.",
                    "DuplicateVersionName");
            }

            // 5. Create new CultivationVersion (will activate at the end)
            var nextOrder = existingVersions.Any() ? existingVersions.Max(v => v.VersionOrder) + 1 : 1;

            var newVersion = new CultivationVersion
            {
                PlotCultivationId = plotCultivation.Id,
                VersionName = request.NewVersionName.Trim(),
                VersionOrder = nextOrder,
                IsActive = false, 
                Reason = request.ResolutionReason?.Trim() ?? $"Emergency response: {emergencyReport.Title}",
                ActivatedAt = null,
                CreatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.Repository<CultivationVersion>().AddAsync(newVersion);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created CultivationVersion '{VersionName}' (Order {Order}) for Plot {PlotId}",
                newVersion.VersionName, newVersion.VersionOrder, plot.Id);

            var cultivationTasks = new List<CultivationTask>();
            var plotArea = plot.Area;

            for (int i = 0; i < request.EmergencyTasks.Count; i++)
            {
                var emergencyTask = request.EmergencyTasks[i];

                var task = new CultivationTask
                {
                    ProductionPlanTaskId = null,
                    PlotCultivationId = plotCultivation.Id,
                    VersionId = newVersion.Id,

                    CultivationTaskName = string.IsNullOrWhiteSpace(emergencyTask.TaskName)
                        ? $"Emergency: {emergencyReport.AlertType} - {plot.SoThua}/{plot.SoTo}"
                        : emergencyTask.TaskName.Trim(),

                    Description = string.IsNullOrWhiteSpace(emergencyTask.Description)
                        ? $"Emergency intervention | Area: {plotArea:F2} ha | Report: {emergencyReport.Title}"
                        : emergencyTask.Description.Trim(),

                    TaskType = emergencyTask.TaskType ?? TaskType.PestControl,
                    Status = TaskStatus.EmergencyApproval,
                    ExecutionOrder = emergencyTask.ExecutionOrder ?? (i + 1),
                    ScheduledEndDate = emergencyTask.ScheduledEndDate ?? DateTime.UtcNow.AddDays(7),
                    
                    AssignedToUserId = emergencyTask.AssignedToUserId,
                    AssignedToVendorId = emergencyTask.AssignedToVendorId,

                    IsContingency = true,
                    ContingencyReason = emergencyTask.ContingencyReason
                        ?? request.ResolutionReason
                        ?? $"Emergency alert: {emergencyReport.AlertType}",

                    CreatedAt = DateTime.UtcNow,
                    
                };

                // Add materials with actual quantities
                foreach (var mat in emergencyTask.Materials)
                {
                    task.CultivationTaskMaterials.Add(new CultivationTaskMaterial
                    {
                        MaterialId = mat.MaterialId,
                        ActualQuantity = mat.Quantity,
                        Notes = mat.Notes ?? $"Emergency use – {emergencyReport.Title}"
                    });
                }

                cultivationTasks.Add(task);
            }

            await _unitOfWork.Repository<CultivationTask>().AddRangeAsync(cultivationTasks);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created {Count} standalone emergency CultivationTasks for Plot {SoThua}/{SoTo} ({Area:F2} ha)",
                cultivationTasks.Count, plot.SoThua, plot.SoTo, plotArea);

            foreach (var oldVersion in existingVersions)
            {
                oldVersion.IsActive = false;
            }

            if (existingVersions.Any())
            {
                _unitOfWork.Repository<CultivationVersion>().UpdateRange(existingVersions);
            }

            newVersion.IsActive = true;
            newVersion.ActivatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<CultivationVersion>().Update(newVersion);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 9. Mark emergency report as resolved
            emergencyReport.Status = AlertStatus.Resolved;
            emergencyReport.ResolvedBy = expertId;
            emergencyReport.ResolvedAt = DateTime.UtcNow;
            emergencyReport.ResolutionNotes =
                $"Emergency resolved by creating version '{newVersion.VersionName}' with {cultivationTasks.Count} standalone tasks " +
                $"on plot {plot.SoThua}/{plot.SoTo} ({plotArea:F2} ha).";

            _unitOfWork.Repository<EmergencyReport>().Update(emergencyReport);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Emergency Report {ReportId} resolved by Expert {ExpertId}. Created version '{VersionName}' with {TaskCount} independent tasks.",
                emergencyReport.Id, expertId, newVersion.VersionName, cultivationTasks.Count);

            return Result<Guid>.Success(
                newVersion.Id,
                $"Emergency plan created successfully. New version '{newVersion.VersionName}' with {cultivationTasks.Count} independent tasks for plot {plot.SoThua}/{plot.SoTo} ({plotArea:F2} ha).");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error during emergency plan creation for Plot {PlotId}", request.PlotId);
            return Result<Guid>.Failure("Database error occurred.", "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during emergency plan creation for Plot {PlotId}", request.PlotId);
            return Result<Guid>.Failure("Failed to create emergency plan.", "CreateEmergencyPlanFailed");
        }
    }
}












