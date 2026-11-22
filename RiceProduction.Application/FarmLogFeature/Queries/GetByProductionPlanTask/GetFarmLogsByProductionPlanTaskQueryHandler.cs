using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByProductionPlanTask;

public class GetFarmLogsByProductionPlanTaskQueryHandler : IRequestHandler<GetFarmLogsByProductionPlanTaskQuery, PagedResult<List<FarmLogDetailResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetFarmLogsByProductionPlanTaskQueryHandler> _logger;

    public GetFarmLogsByProductionPlanTaskQueryHandler(
        IApplicationDbContext context,
        ILogger<GetFarmLogsByProductionPlanTaskQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmLogDetailResponse>>> Handle(
        GetFarmLogsByProductionPlanTaskQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var productionPlanTask = await _context.ProductionPlanTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(ppt => ppt.Id == request.ProductionPlanTaskId, cancellationToken);

            if (productionPlanTask == null)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Failure(
                    "Production Plan Task not found.",
                    "NotFound");
            }

            var cultivationTaskIds = await _context.CultivationTasks
                .AsNoTracking()
                .Where(ct => ct.ProductionPlanTaskId == request.ProductionPlanTaskId)
                .Select(ct => ct.Id)
                .ToListAsync(cancellationToken);

            if (cultivationTaskIds.Count == 0)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Success(
                    new List<FarmLogDetailResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No cultivation tasks found for this production plan task.");
            }

            var logsQuery = _context.FarmLogs
                .AsNoTracking()
                .Where(fl => cultivationTaskIds.Contains(fl.CultivationTaskId));

            var totalCount = await logsQuery.CountAsync(cancellationToken);

            var pagedLogs = await logsQuery
                .Include(fl => fl.CultivationTask)
                    .ThenInclude(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.Plot)
                .Include(fl => fl.FarmLogMaterials)
                    .ThenInclude(flm => flm.Material)
                .OrderByDescending(fl => fl.LoggedDate)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

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
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,

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
