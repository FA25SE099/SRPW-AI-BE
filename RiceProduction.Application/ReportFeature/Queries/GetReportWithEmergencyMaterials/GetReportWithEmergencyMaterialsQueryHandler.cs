using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ReportResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ReportFeature.Queries.GetReportWithEmergencyMaterials;

public class GetReportWithEmergencyMaterialsQueryHandler 
    : IRequestHandler<GetReportWithEmergencyMaterialsQuery, Result<ReportWithEmergencyMaterialsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetReportWithEmergencyMaterialsQueryHandler> _logger;

    public GetReportWithEmergencyMaterialsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetReportWithEmergencyMaterialsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ReportWithEmergencyMaterialsResponse>> Handle(
        GetReportWithEmergencyMaterialsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Load the emergency report with all necessary navigation properties
            var report = await _unitOfWork.Repository<EmergencyReport>()
                .GetQueryable()
                .Include(r => r.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.Farmer)
                .Include(r => r.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
                                .ThenInclude(g => g.Cluster)
                .Include(r => r.Reporter)
                .Include(r => r.Resolver)
                .Include(r => r.Group)
                    .ThenInclude(g => g.Cluster)
                .Include(r => r.Cluster)
                .FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);

            if (report == null)
            {
                return Result<ReportWithEmergencyMaterialsResponse>.Failure(
                    $"Report with ID {request.ReportId} not found.", 
                    "NotFound");
            }

            // 2. Load emergency tasks (Status = Emergency or EmergencyApproval) linked to this report's plot cultivation
            var emergencyTasks = new List<CultivationTask>();
            
            if (report.PlotCultivationId.HasValue)
            {
                emergencyTasks = await _unitOfWork.Repository<CultivationTask>()
                    .GetQueryable()
                    .Include(ct => ct.CultivationTaskMaterials)
                        .ThenInclude(ctm => ctm.Material)
                            .ThenInclude(m => m.MaterialPrices)
                    .Where(ct => ct.PlotCultivationId == report.PlotCultivationId.Value &&
                                (ct.Status == TaskStatus.Emergency || ct.Status == TaskStatus.EmergencyApproval) &&
                                ct.IsContingency)
                    .OrderBy(ct => ct.ExecutionOrder)
                    .ToListAsync(cancellationToken);
            }

            // 3. Calculate material costs
            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();
            var emergencyTaskResponses = new List<EmergencyTaskResponse>();
            decimal totalMaterialCost = 0;

            foreach (var task in emergencyTasks)
            {
                var taskResponse = new EmergencyTaskResponse
                {
                    TaskId = task.Id,
                    TaskName = task.CultivationTaskName ?? "Emergency Task",
                    Description = task.Description,
                    TaskType = task.TaskType,
                    Status = task.Status,
                    ScheduledEndDate = task.ScheduledEndDate,
                    IsContingency = task.IsContingency,
                    ContingencyReason = task.ContingencyReason
                };

                decimal taskTotalCost = 0;

                foreach (var taskMaterial in task.CultivationTaskMaterials)
                {
                    var material = taskMaterial.Material;
                    
                    if (material == null)
                    {
                        priceWarnings.Add($"Material with ID {taskMaterial.MaterialId} not found.");
                        continue;
                    }

                    // Get the current valid price
                    var currentPrice = material.MaterialPrices
                        .Where(p => p.ValidFrom <= currentDate && 
                                   (!p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date))
                        .OrderByDescending(p => p.ValidFrom)
                        .FirstOrDefault();

                    if (currentPrice == null)
                    {
                        priceWarnings.Add($"No valid price found for material '{material.Name}' (ID: {material.Id}).");
                        continue;
                    }

                    // Calculate cost based on actual quantity
                    var amountPerMaterial = material.AmmountPerMaterial.GetValueOrDefault(1M);
                    if (amountPerMaterial <= 0) amountPerMaterial = 1M;

                    var actualQuantity = taskMaterial.ActualQuantity;
                    
                    // Calculate packages needed (ceiling for non-partition materials)
                    var packagesNeeded = material.IsPartition
                        ? actualQuantity / amountPerMaterial
                        : Math.Ceiling(actualQuantity / amountPerMaterial);

                    // Calculate total cost
                    var totalCost = packagesNeeded * currentPrice.PricePerMaterial;

                    var materialResponse = new EmergencyTaskMaterialResponse
                    {
                        MaterialId = material.Id,
                        MaterialName = material.Name,
                        Unit = material.Unit,
                        ActualQuantity = actualQuantity,
                        AmountPerMaterial = amountPerMaterial,
                        PackagesNeeded = packagesNeeded,
                        PricePerMaterial = currentPrice.PricePerMaterial,
                        TotalCost = totalCost,
                        PriceValidFrom = currentPrice.ValidFrom,
                        Notes = taskMaterial.Notes
                    };

                    taskResponse.Materials.Add(materialResponse);
                    taskTotalCost += totalCost;
                }

                taskResponse.TotalTaskMaterialCost = taskTotalCost;
                emergencyTaskResponses.Add(taskResponse);
                totalMaterialCost += taskTotalCost;
            }

            // 4. Build the response
            var response = new ReportWithEmergencyMaterialsResponse
            {
                // Basic report info
                ReportId = report.Id,
                PlotId = report.PlotCultivation?.PlotId,
                PlotName = report.PlotCultivation?.Plot != null
                    ? $"{report.PlotCultivation.Plot.SoThua}/{report.PlotCultivation.Plot.SoTo}"
                    : null,
                PlotArea = report.PlotCultivation?.Plot?.Area,
                CultivationPlanId = report.PlotCultivationId,
                CultivationPlanName = report.PlotCultivation != null
                    ? $"Plan {report.PlotCultivation.PlantingDate:yyyy-MM-dd}"
                    : null,
                ReportType = report.AlertType,
                Severity = report.Severity.ToString(),
                Title = report.Title,
                Description = report.Description,
                ReportedBy = report.Reporter?.FullName ?? "Unknown",
                ReportedByRole = GetReportedByRole(report.Source),
                ReportedAt = report.CreatedAt.DateTime,
                Status = report.Status.ToString(),
                Images = report.ImageUrls,
                Coordinates = report.Coordinates,
                ResolvedBy = report.Resolver?.FullName,
                ResolvedAt = report.ResolvedAt,
                ResolutionNotes = report.ResolutionNotes,
                FarmerName = report.PlotCultivation?.Plot?.Farmer?.FullName,
                ClusterName = report.PlotCultivation?.Plot?.GroupPlots?.FirstOrDefault()?.Group?.Cluster?.ClusterName
                    ?? report.Group?.Cluster?.ClusterName
                    ?? report.Cluster?.ClusterName,

                // Emergency tasks and costs
                EmergencyTasks = emergencyTaskResponses,
                TotalMaterialCost = totalMaterialCost,
                EmergencyTaskCount = emergencyTaskResponses.Count,
                PriceWarnings = priceWarnings
            };

            _logger.LogInformation(
                "Retrieved report {ReportId} with {TaskCount} emergency tasks. Total material cost: {TotalCost}",
                request.ReportId, emergencyTaskResponses.Count, totalMaterialCost);

            return Result<ReportWithEmergencyMaterialsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report {ReportId} with emergency materials", request.ReportId);
            return Result<ReportWithEmergencyMaterialsResponse>.Failure(
                "An error occurred while retrieving the report with emergency materials.");
        }
    }

    private static string? GetReportedByRole(AlertSource source)
    {
        return source switch
        {
            AlertSource.FarmerReport => "Farmer",
            AlertSource.SupervisorInspection => "Supervisor",
            _ => null
        };
    }
}
