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
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateEmergencyPlanForPlotCommandHandler> _logger;

    public CreateEmergencyPlanForPlotCommandHandler(
        IApplicationDbContext context,
        ILogger<CreateEmergencyPlanForPlotCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
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
            var emergencyReport = await _context.EmergencyReports
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

            var plotCultivation = emergencyReport.PlotCultivation!;

            var plan = await _context.ProductionPlans
                .AsNoTracking()
                .Include(p => p.Group)
                    .ThenInclude(g => g!.GroupPlots)
                .FirstOrDefaultAsync(p => p.Id == request.ProductionPlanId, cancellationToken);

            if (plan == null)
            {
                return Result<Guid>.Failure(
                    $"Production Plan with ID {request.ProductionPlanId} not found.",
                    "PlanNotFound");
            }

            var plotBelongsToGroup = plan.Group?.GroupPlots?.Any(gp => gp.PlotId == plot.Id) ?? false;
            if (!plotBelongsToGroup)
            {
                _logger.LogWarning("Plot {PlotId} does not belong to Production Plan {PlanId} group, but continuing for emergency.", plot.Id, plan.Id);
            }

            var requestedMaterialIds = request.EmergencyTasks
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            if (requestedMaterialIds.Any())
            {
                var existingMaterialIds = await _context.Materials
                    .AsNoTracking()
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

            var existingVersions = await _context.CultivationVersions
                .Where(v => v.PlotCultivationId == plotCultivation.Id)
                .ToListAsync(cancellationToken);

            if (existingVersions.Any(v =>
                v.VersionName.Equals(request.NewVersionName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Result<Guid>.Failure(
                    $"Version name '{request.NewVersionName}' already exists for this plot.",
                    "DuplicateVersionName");
            }

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

            _context.CultivationVersions.Add(newVersion);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created CultivationVersion '{VersionName}' (Order {Order}) for Plot {PlotId}",
                newVersion.VersionName, newVersion.VersionOrder, plot.Id);

            var plotArea = plot.Area;
            var cultivationTasks = BuildEmergencyCultivationTasks(
                request, emergencyReport, plot, plotCultivation.Id, newVersion.Id);

            _context.CultivationTasks.AddRange(cultivationTasks);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created {Count} standalone emergency CultivationTasks for Plot {SoThua}/{SoTo} ({Area:F2} ha)",
                cultivationTasks.Count, plot.SoThua, plot.SoTo, plotArea);

            foreach (var oldVersion in existingVersions)
            {
                oldVersion.IsActive = false;
            }

            if (existingVersions.Any())
            {
                _context.CultivationVersions.UpdateRange(existingVersions);
            }

            newVersion.IsActive = true;
            newVersion.ActivatedAt = DateTime.UtcNow;
            _context.CultivationVersions.Update(newVersion);

            await _context.SaveChangesAsync(cancellationToken);

            emergencyReport.Status = AlertStatus.Resolved;
            emergencyReport.ResolvedBy = expertId;
            emergencyReport.ResolvedAt = DateTime.UtcNow;
            emergencyReport.ResolutionNotes =
                $"Emergency resolved by creating version '{newVersion.VersionName}' with {cultivationTasks.Count} standalone tasks " +
                $"on plot {plot.SoThua}/{plot.SoTo} ({plotArea:F2} ha).";

            _context.EmergencyReports.Update(emergencyReport);
            await _context.SaveChangesAsync(cancellationToken);

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

    private static List<CultivationTask> BuildEmergencyCultivationTasks(
        CreateEmergencyPlanForPlotCommand request,
        EmergencyReport emergencyReport,
        Plot plot,
        Guid plotCultivationId,
        Guid versionId)
    {
        var cultivationTasks = new List<CultivationTask>(request.EmergencyTasks.Count);

        for (int i = 0; i < request.EmergencyTasks.Count; i++)
        {
            var emergencyTask = request.EmergencyTasks[i];

            var task = new CultivationTask
            {
                ProductionPlanTaskId = null,
                PlotCultivationId = plotCultivationId,
                VersionId = versionId,

                CultivationTaskName = string.IsNullOrWhiteSpace(emergencyTask.TaskName)
                    ? $"Emergency: {emergencyReport.AlertType} - {plot.SoThua}/{plot.SoTo}"
                    : emergencyTask.TaskName.Trim(),

                Description = string.IsNullOrWhiteSpace(emergencyTask.Description)
                    ? $"Emergency intervention | Area: {plot.Area:F2} ha | Report: {emergencyReport.Title}"
                    : emergencyTask.Description.Trim(),

                TaskType = emergencyTask.TaskType ?? TaskType.PestControl,
                Status = TaskStatus.EmergencyApproval,
                ExecutionOrder = emergencyTask.ExecutionOrder ?? (i + 1),
                ScheduledEndDate = emergencyTask.ScheduledEndDate.HasValue
                    ? DateTime.SpecifyKind(emergencyTask.ScheduledEndDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(7),

                AssignedToUserId = emergencyTask.AssignedToUserId,
                AssignedToVendorId = emergencyTask.AssignedToVendorId,

                IsContingency = true,
                ContingencyReason = emergencyTask.ContingencyReason
                    ?? request.ResolutionReason
                    ?? $"Emergency alert: {emergencyReport.AlertType}",

                CreatedAt = DateTime.UtcNow
            };

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

        return cultivationTasks;
    }
}
