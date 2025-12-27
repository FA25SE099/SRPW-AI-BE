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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResolveReportCommandHandler> _logger;

    public ResolveReportCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ResolveReportCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
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
            var emergencyReport = await _unitOfWork.Repository<EmergencyReport>()
                .GetQueryable()
                .Include(er => er.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .FirstOrDefaultAsync(er => er.Id == request.ReportId, cancellationToken);

            if (emergencyReport == null)
            {
                return Result<Guid>.Failure(
                    $"Emergency Report with ID {request.ReportId} not found.",
                    "EmergencyReportNotFound");
            }

            var plotCultivation = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
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

            var requestedMaterialIds = request.BaseCultivationTasks
                .SelectMany(t => t.MaterialsPerHectare)
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

            var existingVersions = await _unitOfWork.Repository<CultivationVersion>()
                .ListAsync(v => v.PlotCultivationId == plotCultivation.Id);

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

            await _unitOfWork.Repository<CultivationVersion>().AddAsync(newVersion);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var cultivationTasks = new List<CultivationTask>();
            var plotArea = plot.Area;
            
            // Track which tasks are newly created (to skip copying farm logs/late records)
            var newlyCreatedTaskIndices = new HashSet<int>();

            // Get all existing cultivation tasks for the current active version to copy farm logs from
            // (except for Emergency tasks which are brand new problem-solving tasks)
            var currentActiveVersion = existingVersions.FirstOrDefault(v => v.IsActive);
            IReadOnlyList<CultivationTask> oldCultivationTasks = new List<CultivationTask>();
            
            if (currentActiveVersion != null)
            {
                oldCultivationTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                    filter: ct => ct.PlotCultivationId == plotCultivation.Id && ct.VersionId == currentActiveVersion.Id,
                    includeProperties: q => q
                        .Include(ct => ct.FarmLogs)
                            .ThenInclude(fl => fl.FarmLogMaterials)
                        .Include(ct => ct.LateFarmerRecords) // Include late records for copying
                );
            }

            for (int i = 0; i < request.BaseCultivationTasks.Count; i++)
            {
                var taskRequest = request.BaseCultivationTasks[i];
                
                // Check if this is a newly created emergency task
                bool isNewlyCreated = taskRequest.Status == TaskStatus.NewEmergency;
                if (isNewlyCreated)
                {
                    newlyCreatedTaskIndices.Add(i);
                    _logger.LogInformation(
                        "Task at index {Index} is newly created (NewEmergency status). Will skip copying farm logs and late records.",
                        i);
                }

                Guid? productionPlanTaskId = null;
                ProductionPlanTask? productionPlanTask = null;
                Guid? oldCultivationTaskId = taskRequest.CultivationPlanTaskId;
                
                // Frontend sends CultivationPlanTaskId which is actually an existing CultivationTask ID
                // Look it up to get the ProductionPlanTaskId for stage/template information
                if (taskRequest.CultivationPlanTaskId.HasValue)
                {
                    var existingCultivationTask = await _unitOfWork.Repository<CultivationTask>()
                        .FindAsync(
                            match: ct => ct.Id == taskRequest.CultivationPlanTaskId.Value,
                            includeProperties: q => q.Include(ct => ct.ProductionPlanTask)
                        );

                    if (existingCultivationTask != null)
                    {
                        productionPlanTaskId = existingCultivationTask.ProductionPlanTaskId;
                        productionPlanTask = existingCultivationTask.ProductionPlanTask;
                        
                        _logger.LogInformation(
                            "Resolved ProductionPlanTaskId {ProductionPlanTaskId} from CultivationTask {CultivationTaskId} (TaskName: {TaskName})",
                            productionPlanTaskId, taskRequest.CultivationPlanTaskId.Value, existingCultivationTask.CultivationTaskName);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "CultivationTask {CultivationTaskId} not found. Emergency task will be created without ProductionPlanTask reference.",
                            taskRequest.CultivationPlanTaskId.Value);
                    }
                }
                
                // Convert NewEmergency status to Emergency for saving
                var taskStatus = taskRequest.Status == TaskStatus.NewEmergency 
                    ? TaskStatus.Emergency 
                    : (taskRequest.Status ?? TaskStatus.Emergency);

                var newTask = new CultivationTask
                {
                    ProductionPlanTaskId = productionPlanTaskId,
                    PlotCultivationId = plotCultivation.Id,
                    VersionId = newVersion.Id,

                    CultivationTaskName = string.IsNullOrWhiteSpace(taskRequest.TaskName)
                        ? (productionPlanTask?.TaskName ?? $"Emergency: {emergencyReport.AlertType}")
                        : taskRequest.TaskName.Trim(),

                    Description = string.IsNullOrWhiteSpace(taskRequest.Description)
                        ? (productionPlanTask?.Description ?? $"Emergency intervention for {emergencyReport.Title}")
                        : taskRequest.Description.Trim(),

                    TaskType = taskRequest.TaskType ?? productionPlanTask?.TaskType ?? TaskType.PestControl,
                    Status = taskStatus,
                    ExecutionOrder = taskRequest.ExecutionOrder ?? (i + 1),
                    ScheduledEndDate = taskRequest.ScheduledEndDate.HasValue
                        ? DateTime.SpecifyKind(taskRequest.ScheduledEndDate.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow.AddDays(7),
                    ActualStartDate = taskRequest.ActualStartDate.HasValue 
                        ? DateTime.SpecifyKind(taskRequest.ActualStartDate.Value, DateTimeKind.Utc)
                        : null,
                    ActualEndDate = taskRequest.ActualEndDate.HasValue 
                        ? DateTime.SpecifyKind(taskRequest.ActualEndDate.Value, DateTimeKind.Utc)
                        : null,
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
                    var absoluteQuantity = mat.QuantityPerHa * plotArea;

                    newTask.CultivationTaskMaterials.Add(new CultivationTaskMaterial
                    {
                        MaterialId = mat.MaterialId,
                        ActualQuantity = absoluteQuantity,
                        Notes = mat.Notes ?? $"Scaled from {mat.QuantityPerHa}/ha for {plotArea} ha - {emergencyReport.Title}"
                    });
                }

                cultivationTasks.Add(newTask);
                
                // Store mapping for UAV assignment updates later
                if (oldCultivationTaskId.HasValue)
                {
                    // Will use this mapping after tasks are saved and have IDs
                    _logger.LogInformation(
                        "Will update UAV assignments: Old CultivationTask {OldId} ? New CultivationTask (will be assigned after save)",
                        oldCultivationTaskId.Value);
                }
                
                // Log what we're about to save
                _logger.LogInformation(
                    "Creating task {Index}/{Total}: Name='{TaskName}', ProductionPlanTaskId={PPTId}, ExecutionOrder={Order}, Status={Status}, IsNewlyCreated={IsNewlyCreated}",
                    i + 1, request.BaseCultivationTasks.Count, newTask.CultivationTaskName, 
                    productionPlanTaskId, newTask.ExecutionOrder, newTask.Status, isNewlyCreated);
            }

            await _unitOfWork.Repository<CultivationTask>().AddRangeAsync(cultivationTasks);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // VERIFY: Check if ProductionPlanTaskId was actually saved
            var savedTaskIds = cultivationTasks.Select(t => t.Id).ToList();
            var verifyTasks = await _unitOfWork.Repository<CultivationTask>()
                .GetQueryable()
                .Where(t => savedTaskIds.Contains(t.Id))
                .Select(t => new { t.Id, t.CultivationTaskName, t.ProductionPlanTaskId, t.ExecutionOrder })
                .ToListAsync(cancellationToken);
            
            var tasksWithPPTId = verifyTasks.Count(t => t.ProductionPlanTaskId.HasValue);
            var tasksWithoutPPTId = verifyTasks.Count(t => !t.ProductionPlanTaskId.HasValue);
            
            _logger.LogInformation(
                "Verification after save: {TotalTasks} tasks saved. " +
                "With ProductionPlanTaskId: {WithPPTId}, Without: {WithoutPPTId}. " +
                "Sample tasks: {SampleTasks}",
                verifyTasks.Count,
                tasksWithPPTId,
                tasksWithoutPPTId,
                System.Text.Json.JsonSerializer.Serialize(verifyTasks.Take(3))
            );

            // Update UAV assignments to point to new cultivation tasks
            await UpdateUavAssignmentsForNewVersion(
                request.BaseCultivationTasks,
                cultivationTasks,
                cancellationToken);

            // Copy farm logs from old cultivation tasks to new tasks
            // Skip copying for newly created emergency tasks (NewEmergency status)
            if (oldCultivationTasks.Any())
            {
                await CopyFarmLogsToNewTasks(
                    oldCultivationTasks,
                    cultivationTasks,
                    newlyCreatedTaskIndices,
                    plotCultivation.Id,
                    cancellationToken);
                
                // Also copy late farmer records to new tasks
                // Skip copying for newly created emergency tasks (NewEmergency status)
                await CopyLateFarmerRecordsToNewTasks(
                    oldCultivationTasks,
                    cultivationTasks,
                    newlyCreatedTaskIndices,
                    cancellationToken);
            }

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

            emergencyReport.Status = AlertStatus.Resolved;
            emergencyReport.ResolvedBy = expertId;
            emergencyReport.ResolvedAt = DateTime.UtcNow;
            emergencyReport.ResolutionNotes =
                $"Emergency resolved with notes from expert: '{request.ResolutionReason}' " +
                $"Emergency resolved by creating version '{newVersion.VersionName}' with {cultivationTasks.Count} tasks " +
                $"on plot {plot.SoThua}/{plot.SoTo} ({plotArea:F2} ha).";

            _unitOfWork.Repository<EmergencyReport>().Update(emergencyReport);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                
                // Skip newly created emergency tasks - they shouldn't have farm logs copied
                if (newlyCreatedTaskIndices.Contains(i))
                {
                    skippedNewTasksCount++;
                    _logger.LogInformation(
                        "Skipping farm log copy for newly created task {TaskId} at index {Index} (Name: '{TaskName}')",
                        newTask.Id, i, newTask.CultivationTaskName);
                    continue;
                }

                // Skip tasks without ProductionPlanTaskId
                if (!newTask.ProductionPlanTaskId.HasValue)
                {
                    _logger.LogWarning(
                        "New task {TaskId} has no ProductionPlanTaskId. Skipping farm log copy.",
                        newTask.Id);
                    continue;
                }

                // Find the old cultivation task with the same ProductionPlanTaskId
                var oldTask = oldCultivationTasks.FirstOrDefault(
                    ot => ot.ProductionPlanTaskId.HasValue && 
                          ot.ProductionPlanTaskId.Value == newTask.ProductionPlanTaskId.Value);

                if (oldTask == null)
                {
                    _logger.LogInformation(
                        "No old task found with ProductionPlanTaskId {ProductionPlanTaskId} for new task {NewTaskId} (Status: {Status}). Nothing to copy.",
                        newTask.ProductionPlanTaskId.Value, newTask.Id, newTask.Status);
                    continue;
                }

                if (!oldTask.FarmLogs.Any())
                {
                    _logger.LogInformation(
                        "Old task {OldTaskId} has no farm logs. Nothing to copy for new task {NewTaskId} (Status: {Status}).",
                        oldTask.Id, newTask.Id, newTask.Status);
                    continue;
                }

                // Copy all farm logs from old task to new task
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

                    // Copy farm log materials
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
                await _unitOfWork.Repository<FarmLog>().AddRangeAsync(newFarmLogs);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            // Don't throw - this is a nice-to-have feature, shouldn't block the resolution
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
                
                // Skip newly created emergency tasks - they shouldn't have late records copied
                if (newlyCreatedTaskIndices.Contains(i))
                {
                    skippedNewTasksCount++;
                    _logger.LogInformation(
                        "Skipping late record copy for newly created task {TaskId} at index {Index} (Name: '{TaskName}')",
                        newTask.Id, i, newTask.CultivationTaskName);
                    continue;
                }

                // Skip tasks without ProductionPlanTaskId
                if (!newTask.ProductionPlanTaskId.HasValue)
                {
                    _logger.LogWarning(
                        "New task {TaskId} has no ProductionPlanTaskId. Skipping late record copy.",
                        newTask.Id);
                    continue;
                }

                // Find the old cultivation task with the same ProductionPlanTaskId
                var oldTask = oldCultivationTasks.FirstOrDefault(
                    ot => ot.ProductionPlanTaskId.HasValue && 
                          ot.ProductionPlanTaskId.Value == newTask.ProductionPlanTaskId.Value);

                if (oldTask == null)
                {
                    _logger.LogInformation(
                        "No old task found with ProductionPlanTaskId {ProductionPlanTaskId} for new task {NewTaskId}. Nothing to copy.",
                        newTask.ProductionPlanTaskId.Value, newTask.Id);
                    continue;
                }

                if (!oldTask.LateFarmerRecords.Any())
                {
                    _logger.LogInformation(
                        "Old task {OldTaskId} has no late records. Nothing to copy for new task {NewTaskId}.",
                        oldTask.Id, newTask.Id);
                    continue;
                }

                // Copy all late farmer records from old task to new task
                foreach (var oldRecord in oldTask.LateFarmerRecords)
                {
                    var newRecord = new LateFarmerRecord
                    {
                        FarmerId = oldRecord.FarmerId,
                        CultivationTaskId = newTask.Id,
                        RecordedAt = oldRecord.RecordedAt,
                        Notes = oldRecord.Notes + " [Copied from previous version]",
                        CreatedAt = DateTime.UtcNow
                    };

                    newLateRecords.Add(newRecord);
                    copiedRecordsCount++;
                }

                _logger.LogInformation(
                    "Copied {Count} late records from old task {OldTaskId} to new task {NewTaskId}",
                    oldTask.LateFarmerRecords.Count, oldTask.Id, newTask.Id);
            }

            if (newLateRecords.Any())
            {
                await _unitOfWork.Repository<LateFarmerRecord>().AddRangeAsync(newLateRecords);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            // Don't throw - this is a nice-to-have feature, shouldn't block the resolution
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
                var uavAssignments = await _unitOfWork.Repository<UavOrderPlotAssignment>()
                    .GetQueryable()
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
                
                _unitOfWork.Repository<UavOrderPlotAssignment>().UpdateRange(uavAssignments);
            }
            
            if (updatedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
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

