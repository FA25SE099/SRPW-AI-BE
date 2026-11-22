using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ReportFeature.Command;

public class ResolveReportCommandHandler : IRequestHandler<ResolveReportCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ResolveReportCommandHandler> _logger;

    public ResolveReportCommandHandler(
        IApplicationDbContext context,
        ILogger<ResolveReportCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(ResolveReportCommand request, CancellationToken cancellationToken)
    {
        var expertId = request.ExpertId;
        if (!expertId.HasValue)
        {
            return Result<Guid>.Failure("Current expert user ID not found.", "AuthenticationRequired");
        }

        try
        {
            var emergencyReport = await _context.EmergencyReports
                .Include(er => er.PlotCultivation)
                    .ThenInclude(pc => pc!.Plot)
                .FirstOrDefaultAsync(er => er.Id == request.ReportId, cancellationToken);

            if (emergencyReport == null)
            {
                return Result<Guid>.Failure(
                    $"Emergency Report with ID {request.ReportId} not found.",
                    "EmergencyReportNotFound");
            }

            var plotCultivation = await _context.PlotCultivations
                .Include(pc => pc.Plot)
                .FirstOrDefaultAsync(pc => pc.Id == request.CultivationPlanId, cancellationToken);

            if (plotCultivation == null)
            {
                return Result<Guid>.Failure(
                    $"Cultivation plan with ID {request.CultivationPlanId} not found.",
                    "CultivationPlanNotFound");
            }

            var plot = plotCultivation.Plot;
            if (plot == null)
            {
                return Result<Guid>.Failure("Plot not found for this cultivation plan.", "PlotNotFound");
            }

            var materialValidationResult = await ValidateMaterialsAsync(request.BaseCultivationTasks, cancellationToken);
            if (materialValidationResult != null)
            {
                return materialValidationResult;
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

            var currentMaxOrder = existingVersions.Any() ? existingVersions.Max(v => v.VersionOrder) : 0;

            var newVersion = CultivationVersion.CreateNext(
                plotCultivation.Id,
                currentMaxOrder,
                request.NewVersionName.Trim(),
                reason: request.ResolutionReason?.Trim() ?? $"Emergency response: {emergencyReport.Title}",
                activateImmediately: false);
            newVersion.CreatedAt = DateTime.UtcNow;

            _context.CultivationVersions.Add(newVersion);
            await _context.SaveChangesAsync(cancellationToken);

            var plotArea = plot.Area;
            var currentActiveVersion = existingVersions.FirstOrDefault(v => v.IsActive);
            var oldCultivationTasks = await LoadActiveVersionTasksAsync(plotCultivation.Id, currentActiveVersion, cancellationToken);

            var (cultivationTasks, newlyCreatedTaskIndices) = await BuildCultivationTasksAsync(
                request, emergencyReport, plotCultivation.Id, newVersion.Id, plotArea, cancellationToken);

            _context.CultivationTasks.AddRange(cultivationTasks);
            await _context.SaveChangesAsync(cancellationToken);

            await VerifySavedTasksAsync(cultivationTasks, cancellationToken);

            await UpdateUavAssignmentsForNewVersion(
                request.BaseCultivationTasks,
                cultivationTasks,
                cancellationToken);

            if (oldCultivationTasks.Any())
            {
                await CopyFarmLogsToNewTasks(
                    oldCultivationTasks,
                    cultivationTasks,
                    newlyCreatedTaskIndices,
                    plotCultivation.Id,
                    cancellationToken);

                await CopyLateFarmerRecordsToNewTasks(
                    oldCultivationTasks,
                    cultivationTasks,
                    newlyCreatedTaskIndices,
                    cancellationToken);
            }

            await ActivateNewVersionAsync(existingVersions, newVersion, cancellationToken);

            emergencyReport.Status = AlertStatus.Resolved;
            emergencyReport.ResolvedBy = expertId;
            emergencyReport.ResolvedAt = DateTime.UtcNow;
            emergencyReport.ResolutionNotes =
                $"Emergency resolved with notes from expert: '{request.ResolutionReason}' " +
                $"Emergency resolved by creating version '{newVersion.VersionName}' with {cultivationTasks.Count} tasks " +
                $"on plot {plot.SoThua}/{plot.SoTo} ({plotArea:F2} ha).";

            _context.EmergencyReports.Update(emergencyReport);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Emergency Report {ReportId} resolved by Expert {ExpertId}. Created version '{VersionName}' with {TaskCount} tasks.",
                emergencyReport.Id, expertId, newVersion.VersionName, cultivationTasks.Count);

            return Result<Guid>.Success(
                newVersion.Id,
                $"Emergency plan created successfully. New version '{newVersion.VersionName}' with {cultivationTasks.Count} tasks.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error during report resolution");
            return Result<Guid>.Failure("Database error occurred.", "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during report resolution");
            return Result<Guid>.Failure("Failed to resolve report.", "ResolveReportFailed");
        }
    }

    private async Task<Result<Guid>?> ValidateMaterialsAsync(
        List<BaseCultivationTaskRequest> taskRequests,
        CancellationToken cancellationToken)
    {
        var requestedMaterialIds = taskRequests
            .SelectMany(t => t.MaterialsPerHectare)
            .Select(m => m.MaterialId)
            .Distinct()
            .ToList();

        if (!requestedMaterialIds.Any())
        {
            return null;
        }

        var existingMaterialIds = await _context.Materials
            .AsNoTracking()
            .Where(m => requestedMaterialIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var missing = requestedMaterialIds.Except(existingMaterialIds).ToList();
        return missing.Any()
            ? Result<Guid>.Failure($"Materials not found: {string.Join(", ", missing)}", "MaterialsNotFound")
            : null;
    }

    private async Task<IReadOnlyList<CultivationTask>> LoadActiveVersionTasksAsync(
        Guid plotCultivationId,
        CultivationVersion? currentActiveVersion,
        CancellationToken cancellationToken)
    {
        if (currentActiveVersion == null)
        {
            return Array.Empty<CultivationTask>();
        }

        return await _context.CultivationTasks
            .AsNoTracking()
            .AsSplitQuery()
            .Include(ct => ct.FarmLogs)
                .ThenInclude(fl => fl.FarmLogMaterials)
            .Include(ct => ct.LateFarmerRecords)
            .Where(ct => ct.PlotCultivationId == plotCultivationId && ct.VersionId == currentActiveVersion.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<(List<CultivationTask> Tasks, HashSet<int> NewlyCreatedIndices)> BuildCultivationTasksAsync(
        ResolveReportCommand request,
        EmergencyReport emergencyReport,
        Guid plotCultivationId,
        Guid newVersionId,
        decimal plotArea,
        CancellationToken cancellationToken)
    {
        var cultivationTasks = new List<CultivationTask>();
        var newlyCreatedTaskIndices = new HashSet<int>();

        for (int i = 0; i < request.BaseCultivationTasks.Count; i++)
        {
            var taskRequest = request.BaseCultivationTasks[i];
            var isNewlyCreated = taskRequest.Status == TaskStatus.NewEmergency;

            if (isNewlyCreated)
            {
                newlyCreatedTaskIndices.Add(i);
                _logger.LogInformation(
                    "Task at index {Index} is newly created (NewEmergency status). Will skip copying farm logs and late records.",
                    i);
            }

            var (productionPlanTaskId, productionPlanTask) = await ResolveProductionPlanTaskAsync(
                taskRequest.CultivationPlanTaskId, cancellationToken);

            // Convert NewEmergency status to Emergency for saving
            var taskStatus = isNewlyCreated
                ? TaskStatus.Emergency
                : taskRequest.Status ?? TaskStatus.Emergency;

            var newTask = new CultivationTask
            {
                ProductionPlanTaskId = productionPlanTaskId,
                PlotCultivationId = plotCultivationId,
                VersionId = newVersionId,
                CultivationTaskName = string.IsNullOrWhiteSpace(taskRequest.TaskName)
                    ? productionPlanTask?.TaskName ?? $"Emergency: {emergencyReport.AlertType}"
                    : taskRequest.TaskName.Trim(),
                Description = string.IsNullOrWhiteSpace(taskRequest.Description)
                    ? productionPlanTask?.Description ?? $"Emergency intervention for {emergencyReport.Title}"
                    : taskRequest.Description.Trim(),
                TaskType = taskRequest.TaskType ?? productionPlanTask?.TaskType ?? TaskType.PestControl,
                Status = taskStatus,
                ExecutionOrder = taskRequest.ExecutionOrder ?? (i + 1),
                ScheduledEndDate = SpecifyUtc(taskRequest.ScheduledEndDate) ?? DateTime.UtcNow.AddDays(7),
                ActualStartDate = SpecifyUtc(taskRequest.ActualStartDate),
                ActualEndDate = SpecifyUtc(taskRequest.ActualEndDate),
                AssignedToUserId = taskRequest.DefaultAssignedToUserId,
                AssignedToVendorId = taskRequest.DefaultAssignedToVendorId,
                IsContingency = true,
                ContingencyReason = taskRequest.ContingencyReason
                    ?? request.ResolutionReason
                    ?? $"Emergency alert: {emergencyReport.AlertType}",
                CreatedAt = DateTime.UtcNow,
            };

            foreach (var mat in taskRequest.MaterialsPerHectare)
            {
                newTask.CultivationTaskMaterials.Add(new CultivationTaskMaterial
                {
                    MaterialId = mat.MaterialId,
                    ActualQuantity = mat.QuantityPerHa * plotArea,
                    Notes = mat.Notes ?? $"Scaled from {mat.QuantityPerHa}/ha for {plotArea} ha - {emergencyReport.Title}"
                });
            }

            cultivationTasks.Add(newTask);

            if (taskRequest.CultivationPlanTaskId.HasValue)
            {
                _logger.LogInformation(
                    "Will update UAV assignments: Old CultivationTask {OldId} ? New CultivationTask (will be assigned after save)",
                    taskRequest.CultivationPlanTaskId.Value);
            }

            _logger.LogInformation(
                "Creating task {Index}/{Total}: Name='{TaskName}', ProductionPlanTaskId={PPTId}, ExecutionOrder={Order}, Status={Status}, IsNewlyCreated={IsNewlyCreated}",
                i + 1, request.BaseCultivationTasks.Count, newTask.CultivationTaskName,
                productionPlanTaskId, newTask.ExecutionOrder, newTask.Status, isNewlyCreated);
        }

        return (cultivationTasks, newlyCreatedTaskIndices);
    }

    private async Task<(Guid? ProductionPlanTaskId, ProductionPlanTask? ProductionPlanTask)> ResolveProductionPlanTaskAsync(
        Guid? cultivationPlanTaskId,
        CancellationToken cancellationToken)
    {
        if (!cultivationPlanTaskId.HasValue)
        {
            return (null, null);
        }

        // Frontend sends CultivationPlanTaskId which is actually an existing CultivationTask ID
        var existingCultivationTask = await _context.CultivationTasks
            .AsNoTracking()
            .Include(ct => ct.ProductionPlanTask)
            .FirstOrDefaultAsync(ct => ct.Id == cultivationPlanTaskId.Value, cancellationToken);

        if (existingCultivationTask != null)
        {
            _logger.LogInformation(
                "Resolved ProductionPlanTaskId {ProductionPlanTaskId} from CultivationTask {CultivationTaskId} (TaskName: {TaskName})",
                existingCultivationTask.ProductionPlanTaskId,
                cultivationPlanTaskId.Value,
                existingCultivationTask.CultivationTaskName);

            return (existingCultivationTask.ProductionPlanTaskId, existingCultivationTask.ProductionPlanTask);
        }

        _logger.LogWarning(
            "CultivationTask {CultivationTaskId} not found. Emergency task will be created without ProductionPlanTask reference.",
            cultivationPlanTaskId.Value);

        return (null, null);
    }

    private async Task VerifySavedTasksAsync(List<CultivationTask> cultivationTasks, CancellationToken cancellationToken)
    {
        var savedTaskIds = cultivationTasks.Select(t => t.Id).ToList();
        var verifyTasks = await _context.CultivationTasks
            .AsNoTracking()
            .Where(t => savedTaskIds.Contains(t.Id))
            .Select(t => new { t.Id, t.CultivationTaskName, t.ProductionPlanTaskId, t.ExecutionOrder })
            .ToListAsync(cancellationToken);

        var tasksWithPPTId = verifyTasks.Count(t => t.ProductionPlanTaskId.HasValue);
        var tasksWithoutPPTId = verifyTasks.Count - tasksWithPPTId;

        _logger.LogInformation(
            "Verification after save: {TotalTasks} tasks saved. " +
            "With ProductionPlanTaskId: {WithPPTId}, Without: {WithoutPPTId}. " +
            "Sample tasks: {SampleTasks}",
            verifyTasks.Count,
            tasksWithPPTId,
            tasksWithoutPPTId,
            System.Text.Json.JsonSerializer.Serialize(verifyTasks.Take(3)));
    }

    private async Task ActivateNewVersionAsync(
        IReadOnlyList<CultivationVersion> existingVersions,
        CultivationVersion newVersion,
        CancellationToken cancellationToken)
    {
        foreach (var oldVersion in existingVersions)
        {
            oldVersion.Deactivate();
        }

        if (existingVersions.Any())
        {
            _context.CultivationVersions.UpdateRange(existingVersions);
        }

        newVersion.Activate();
        _context.CultivationVersions.Update(newVersion);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static DateTime? SpecifyUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private (CultivationTask? OldTask, bool SkippedAsNew) MatchOldTaskForCopy(
        IReadOnlyList<CultivationTask> oldCultivationTasks,
        CultivationTask newTask,
        int index,
        HashSet<int> newlyCreatedTaskIndices,
        string recordLabel)
    {
        if (newlyCreatedTaskIndices.Contains(index))
        {
            _logger.LogInformation(
                "Skipping {RecordLabel} copy for newly created task {TaskId} at index {Index} (Name: '{TaskName}')",
                recordLabel, newTask.Id, index, newTask.CultivationTaskName);
            return (null, true);
        }

        if (!newTask.ProductionPlanTaskId.HasValue)
        {
            _logger.LogWarning(
                "New task {TaskId} has no ProductionPlanTaskId. Skipping {RecordLabel} copy.",
                newTask.Id, recordLabel);
            return (null, false);
        }

        var oldTask = oldCultivationTasks.FirstOrDefault(
            ot => ot.ProductionPlanTaskId.HasValue &&
                  ot.ProductionPlanTaskId.Value == newTask.ProductionPlanTaskId.Value);

        if (oldTask == null)
        {
            _logger.LogInformation(
                "No old task found with ProductionPlanTaskId {ProductionPlanTaskId} for new task {NewTaskId} (Status: {Status}). Nothing to copy.",
                newTask.ProductionPlanTaskId.Value, newTask.Id, newTask.Status);
        }

        return (oldTask, false);
    }

    /// <summary>
    /// Copies farm logs from old cultivation tasks to new cultivation tasks based on ProductionPlanTaskId matching.
    /// Skips copying for newly created emergency tasks (those sent with NewEmergency status).
    /// </summary>
    private async Task CopyFarmLogsToNewTasks(
        IReadOnlyList<CultivationTask> oldCultivationTasks,
        List<CultivationTask> newCultivationTasks,
        HashSet<int> newlyCreatedTaskIndices,
        Guid plotCultivationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var newFarmLogs = new List<FarmLog>();
            int copiedLogsCount = 0;
            int skippedNewTasksCount = 0;

            for (int i = 0; i < newCultivationTasks.Count; i++)
            {
                var newTask = newCultivationTasks[i];
                var (oldTask, skippedAsNew) = MatchOldTaskForCopy(
                    oldCultivationTasks, newTask, i, newlyCreatedTaskIndices, "farm log");

                if (skippedAsNew)
                {
                    skippedNewTasksCount++;
                    continue;
                }

                if (oldTask == null)
                {
                    continue;
                }

                if (!oldTask.FarmLogs.Any())
                {
                    _logger.LogInformation(
                        "Old task {OldTaskId} has no farm logs. Nothing to copy for new task {NewTaskId} (Status: {Status}).",
                        oldTask.Id, newTask.Id, newTask.Status);
                    continue;
                }

                foreach (var oldLog in oldTask.FarmLogs)
                {
                    var newLog = new FarmLog
                    {
                        CultivationTaskId = newTask.Id,
                        PlotCultivationId = plotCultivationId,
                        LoggedBy = oldLog.LoggedBy,
                        LoggedDate = oldLog.LoggedDate,
                        WorkDescription = oldLog.WorkDescription + " [Copied from previous version]",
                        CompletionPercentage = oldLog.CompletionPercentage,
                        ActualAreaCovered = oldLog.ActualAreaCovered,
                        ActualMaterialJson = oldLog.ActualMaterialJson,
                        ServiceCost = oldLog.ServiceCost,
                        ServiceNotes = oldLog.ServiceNotes,
                        PhotoUrls = oldLog.PhotoUrls,
                        WeatherConditions = oldLog.WeatherConditions,
                        InterruptionReason = oldLog.InterruptionReason,
                        VerifiedBy = oldLog.VerifiedBy,
                        VerifiedAt = oldLog.VerifiedAt,
                        CreatedAt = DateTime.UtcNow
                    };

                    foreach (var oldMaterial in oldLog.FarmLogMaterials)
                    {
                        newLog.FarmLogMaterials.Add(new FarmLogMaterial
                        {
                            MaterialId = oldMaterial.MaterialId,
                            ActualQuantityUsed = oldMaterial.ActualQuantityUsed,
                            ActualCost = oldMaterial.ActualCost,
                            Notes = oldMaterial.Notes + " [Copied from previous version]"
                        });
                    }

                    newFarmLogs.Add(newLog);
                    copiedLogsCount++;
                }

                _logger.LogInformation(
                    "Copied {Count} farm logs from old task {OldTaskId} (ProductionPlanTaskId: {ProductionPlanTaskId}) to new task {NewTaskId} (Status: {Status})",
                    oldTask.FarmLogs.Count, oldTask.Id, oldTask.ProductionPlanTaskId, newTask.Id, newTask.Status);
            }

            if (newFarmLogs.Any())
            {
                _context.FarmLogs.AddRange(newFarmLogs);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully copied total {Count} farm logs to {TaskCount} tasks. Skipped {SkippedCount} newly created tasks.",
                    copiedLogsCount, newCultivationTasks.Count - skippedNewTasksCount, skippedNewTasksCount);
            }
            else
            {
                _logger.LogInformation(
                    "No farm logs were copied - no matching old tasks with logs found. Skipped {SkippedCount} newly created tasks.",
                    skippedNewTasksCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error copying farm logs to new tasks. Continuing with resolution.");
            // Don't throw - copying logs shouldn't block emergency resolution
        }
    }

    /// <summary>
    /// Copies late farmer records from old cultivation tasks to new cultivation tasks based on ProductionPlanTaskId matching.
    /// Skips copying for newly created emergency tasks (those sent with NewEmergency status).
    /// This preserves the lateness history when creating new versions.
    /// </summary>
    private async Task CopyLateFarmerRecordsToNewTasks(
        IReadOnlyList<CultivationTask> oldCultivationTasks,
        List<CultivationTask> newCultivationTasks,
        HashSet<int> newlyCreatedTaskIndices,
        CancellationToken cancellationToken)
    {
        try
        {
            var newLateRecords = new List<LateFarmerRecord>();
            int copiedRecordsCount = 0;
            int skippedNewTasksCount = 0;

            for (int i = 0; i < newCultivationTasks.Count; i++)
            {
                var newTask = newCultivationTasks[i];
                var (oldTask, skippedAsNew) = MatchOldTaskForCopy(
                    oldCultivationTasks, newTask, i, newlyCreatedTaskIndices, "late record");

                if (skippedAsNew)
                {
                    skippedNewTasksCount++;
                    continue;
                }

                if (oldTask == null)
                {
                    continue;
                }

                if (!oldTask.LateFarmerRecords.Any())
                {
                    _logger.LogInformation(
                        "Old task {OldTaskId} has no late records. Nothing to copy for new task {NewTaskId}.",
                        oldTask.Id, newTask.Id);
                    continue;
                }

                foreach (var oldRecord in oldTask.LateFarmerRecords)
                {
                    newLateRecords.Add(new LateFarmerRecord
                    {
                        FarmerId = oldRecord.FarmerId,
                        CultivationTaskId = newTask.Id,
                        RecordedAt = oldRecord.RecordedAt,
                        Notes = oldRecord.Notes + " [Copied from previous version]",
                        CreatedAt = DateTime.UtcNow
                    });
                    copiedRecordsCount++;
                }

                _logger.LogInformation(
                    "Copied {Count} late records from old task {OldTaskId} to new task {NewTaskId}",
                    oldTask.LateFarmerRecords.Count, oldTask.Id, newTask.Id);
            }

            if (newLateRecords.Any())
            {
                _context.LateFarmerRecords.AddRange(newLateRecords);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully copied total {Count} late farmer records to {TaskCount} tasks. Skipped {SkippedCount} newly created tasks.",
                    copiedRecordsCount, newCultivationTasks.Count - skippedNewTasksCount, skippedNewTasksCount);
            }
            else
            {
                _logger.LogInformation(
                    "No late farmer records were copied - no matching old tasks with records found. Skipped {SkippedCount} newly created tasks.",
                    skippedNewTasksCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error copying late farmer records to new tasks. Continuing with resolution.");
            // Don't throw - copying late records shouldn't block emergency resolution
        }
    }

    /// <summary>
    /// Updates UAV service order plot assignments to reference new cultivation task IDs.
    /// When creating a new version, UAV assignments that pointed to old tasks need to be updated
    /// to point to the corresponding new tasks in the new version.
    /// </summary>
    private async Task UpdateUavAssignmentsForNewVersion(
        List<BaseCultivationTaskRequest> taskRequests,
        List<CultivationTask> newTasks,
        CancellationToken cancellationToken)
    {
        try
        {
            int updatedCount = 0;
            
            for (int i = 0; i < taskRequests.Count; i++)
            {
                var taskRequest = taskRequests[i];
                
                // Skip if no old cultivation task ID provided
                if (!taskRequest.CultivationPlanTaskId.HasValue)
                {
                    continue;
                }
                
                var oldCultivationTaskId = taskRequest.CultivationPlanTaskId.Value;
                var newCultivationTask = newTasks[i]; // Same index as request
                
                // Find all UAV assignments that reference the old cultivation task
                var uavAssignments = await _context.UavOrderPlotAssignments
                    .Where(ua => ua.CultivationTaskId == oldCultivationTaskId)
                    .ToListAsync(cancellationToken);
                
                if (!uavAssignments.Any())
                {
                    _logger.LogInformation(
                        "No UAV assignments found for old CultivationTask {OldTaskId}",
                        oldCultivationTaskId);
                    continue;
                }
                
                // Update each assignment to point to the new cultivation task
                foreach (var assignment in uavAssignments)
                {
                    assignment.CultivationTaskId = newCultivationTask.Id;
                    updatedCount++;
                    
                    _logger.LogInformation(
                        "Updated UAV assignment {AssignmentId}: Old task {OldTaskId} ? New task {NewTaskId}",
                        assignment.Id, oldCultivationTaskId, newCultivationTask.Id);
                }
                
                _context.UavOrderPlotAssignments.UpdateRange(uavAssignments);
            }
            
            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation(
                    "Successfully updated {Count} UAV assignments to reference new cultivation tasks",
                    updatedCount);
            }
            else
            {
                _logger.LogInformation("No UAV assignments needed updating");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error updating UAV assignments for new version. Continuing with resolution.");
            // Don't throw - this is a nice-to-have feature, shouldn't block the resolution
        }
    }
}

