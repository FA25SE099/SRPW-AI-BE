using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByProductionPlanTask;

public class GetFarmLogsByProductionPlanTaskQueryHandler : IRequestHandler<GetFarmLogsByProductionPlanTaskQuery, PagedResult<List<FarmLogDetailResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetFarmLogsByProductionPlanTaskQueryHandler> _logger;

    public GetFarmLogsByProductionPlanTaskQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetFarmLogsByProductionPlanTaskQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmLogDetailResponse>>> Handle(
        GetFarmLogsByProductionPlanTaskQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify ProductionPlanTask exists
            var productionPlanTask = await _unitOfWork.Repository<ProductionPlanTask>()
                .FindAsync(ppt => ppt.Id == request.ProductionPlanTaskId);

            if (productionPlanTask == null)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Failure(
                    "Production Plan Task not found.",
                    "NotFound");
            }

            // Get all CultivationTask IDs for this ProductionPlanTask
            var cultivationTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.ProductionPlanTaskId == request.ProductionPlanTaskId,
                includeProperties: q => q.Include(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
            );

            if (!cultivationTasks.Any())
            {
                return PagedResult<List<FarmLogDetailResponse>>.Success(
                    new List<FarmLogDetailResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No cultivation tasks found for this production plan task.");
            }

            var cultivationTaskIds = cultivationTasks.Select(ct => ct.Id).ToList();

            // Get all farm logs for these cultivation tasks
            var allLogs = await _unitOfWork.Repository<FarmLog>().ListAsync(
                filter: fl => cultivationTaskIds.Contains(fl.CultivationTaskId),
                orderBy: q => q.OrderByDescending(fl => fl.LoggedDate),
                includeProperties: q => q
                    .Include(fl => fl.CultivationTask)
                        .ThenInclude(ct => ct.PlotCultivation)
                            .ThenInclude(pc => pc.Plot)
                    .Include(fl => fl.FarmLogMaterials)
                        .ThenInclude(flm => flm.Material)
            );

            var totalCount = allLogs.Count;

            // Apply pagination
            var pagedLogs = allLogs
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Map to response
            var responseData = pagedLogs.Select(fl =>
            {
                var plot = fl.CultivationTask.PlotCultivation.Plot;
                return new FarmLogDetailResponse
                {
                    FarmLogId = fl.Id,
                    LoggedDate = fl.LoggedDate,
                    WorkDescription = fl.WorkDescription,
                    CompletionPercentage = fl.CompletionPercentage,
                    ActualAreaCovered = fl.ActualAreaCovered,
                    ServiceCost = fl.ServiceCost,
                    ServiceNotes = fl.ServiceNotes,
                    PhotoUrls = fl.PhotoUrls,
                    WeatherConditions = fl.WeatherConditions,
                    InterruptionReason = fl.InterruptionReason,

                    CultivationTaskName = fl.CultivationTask.CultivationTaskName ?? productionPlanTask.TaskName,
                    PlotName = $"Th?a {plot.SoThua ?? 0}, T? {plot.SoTo ?? 0}",

                    MaterialsUsed = fl.FarmLogMaterials.Select(flm => new FarmLogMaterialRecord
                    {
                        MaterialName = flm.Material.Name,
                        ActualQuantityUsed = flm.ActualQuantityUsed,
                        ActualCost = flm.ActualCost,
                        Notes = flm.Notes
                    }).ToList()
                };
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} farm logs for production plan task {TaskId}",
                responseData.Count,
                request.ProductionPlanTaskId);

            return PagedResult<List<FarmLogDetailResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved farm logs by production plan task.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving farm logs for production plan task {TaskId}",
                request.ProductionPlanTaskId);
            return PagedResult<List<FarmLogDetailResponse>>.Failure(
                "An error occurred while retrieving farm logs.",
                "GetFarmLogsFailed");
        }
    }
}
