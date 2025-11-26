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

            for (int i = 0; i < request.BaseCultivationTasks.Count; i++)
            {
                var taskRequest = request.BaseCultivationTasks[i];

                ProductionPlanTask? productionPlanTask = null;
                if (taskRequest.CultivationPlanTaskId.HasValue)
                {
                    productionPlanTask = await _unitOfWork.Repository<ProductionPlanTask>()
                        .GetQueryable()
                        .FirstOrDefaultAsync(ppt => ppt.Id == taskRequest.CultivationPlanTaskId.Value, cancellationToken);

                    if (productionPlanTask == null)
                    {
                        return Result<Guid>.Failure(
                            $"Production plan task with ID {taskRequest.CultivationPlanTaskId.Value} not found.",
                            "ProductionPlanTaskNotFound");
                    }
                }

                var task = new CultivationTask
                {
                    ProductionPlanTaskId = taskRequest.CultivationPlanTaskId,
                    PlotCultivationId = plotCultivation.Id,
                    VersionId = newVersion.Id,

                    CultivationTaskName = string.IsNullOrWhiteSpace(taskRequest.TaskName)
                        ? (productionPlanTask?.TaskName ?? $"Emergency: {emergencyReport.AlertType}")
                        : taskRequest.TaskName.Trim(),

                    Description = string.IsNullOrWhiteSpace(taskRequest.Description)
                        ? (productionPlanTask?.Description ?? $"Emergency intervention for {emergencyReport.Title}")
                        : taskRequest.Description.Trim(),

                    TaskType = taskRequest.TaskType ?? productionPlanTask?.TaskType ?? TaskType.PestControl,
                    Status = taskRequest.Status ?? TaskStatus.Draft,
                    ExecutionOrder = taskRequest.ExecutionOrder ?? (i + 1),
                    ScheduledEndDate = taskRequest.ScheduledEndDate ?? DateTime.UtcNow.AddDays(7),

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

                    task.CultivationTaskMaterials.Add(new CultivationTaskMaterial
                    {
                        MaterialId = mat.MaterialId,
                        ActualQuantity = absoluteQuantity,
                        Notes = mat.Notes ?? $"Scaled from {mat.QuantityPerHa}/ha for {plotArea} ha - {emergencyReport.Title}"
                    });
                }

                cultivationTasks.Add(task);
            }

            await _unitOfWork.Repository<CultivationTask>().AddRangeAsync(cultivationTasks);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
}

