using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationTask;

public class GetFarmLogsByCultivationTaskQueryHandler : IRequestHandler<GetFarmLogsByCultivationTaskQuery, PagedResult<List<FarmLogDetailResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetFarmLogsByCultivationTaskQueryHandler> _logger;

    public GetFarmLogsByCultivationTaskQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetFarmLogsByCultivationTaskQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmLogDetailResponse>>> Handle(
        GetFarmLogsByCultivationTaskQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify CultivationTask exists and get it with related entities
            var cultivationTask = await _unitOfWork.Repository<CultivationTask>().FindAsync(
                ct => ct.Id == request.CultivationTaskId,
                includeProperties: q => q
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.Plot)
                    .Include(ct => ct.ProductionPlanTask)
            );

            if (cultivationTask == null)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Failure(
                    "Cultivation Task not found.",
                    "NotFound");
            }

            // Get all farm logs for this specific cultivation task
            var allLogs = await _unitOfWork.Repository<FarmLog>().ListAsync(
                filter: fl => fl.CultivationTaskId == request.CultivationTaskId,
                orderBy: q => q.OrderByDescending(fl => fl.LoggedDate),
                includeProperties: q => q
                    .Include(fl => fl.FarmLogMaterials)
                        .ThenInclude(flm => flm.Material)
                    .Include(fl => fl.Logger)
                    .Include(fl => fl.Verifier)
            );

            var totalCount = allLogs.Count;

            // Apply pagination
            var pagedLogs = allLogs
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Map to response
            var plot = cultivationTask.PlotCultivation.Plot;
            var taskName = cultivationTask.CultivationTaskName 
                ?? cultivationTask.ProductionPlanTask?.TaskName 
                ?? "Unknown Task";

            var responseData = pagedLogs.Select(fl => new FarmLogDetailResponse
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

                CultivationTaskName = taskName,
                SoThua = plot.SoThua,
                SoTo = plot.SoTo,

                MaterialsUsed = fl.FarmLogMaterials.Select(flm => new FarmLogMaterialRecord
                {
                    MaterialName = flm.Material.Name,
                    ActualQuantityUsed = flm.ActualQuantityUsed,
                    ActualCost = flm.ActualCost,
                    Notes = flm.Notes
                }).ToList()
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} farm logs for cultivation task {TaskId}",
                responseData.Count,
                request.CultivationTaskId);

            return PagedResult<List<FarmLogDetailResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved farm logs for cultivation task.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving farm logs for cultivation task {TaskId}",
                request.CultivationTaskId);
            return PagedResult<List<FarmLogDetailResponse>>.Failure(
                "An error occurred while retrieving farm logs.",
                "GetFarmLogsFailed");
        }
    }
}
