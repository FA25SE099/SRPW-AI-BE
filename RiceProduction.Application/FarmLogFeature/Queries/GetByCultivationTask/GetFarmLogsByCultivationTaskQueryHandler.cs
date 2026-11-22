using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationTask;

public class GetFarmLogsByCultivationTaskQueryHandler : IRequestHandler<GetFarmLogsByCultivationTaskQuery, PagedResult<List<FarmLogDetailResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetFarmLogsByCultivationTaskQueryHandler> _logger;

    public GetFarmLogsByCultivationTaskQueryHandler(
        IApplicationDbContext context,
        ILogger<GetFarmLogsByCultivationTaskQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmLogDetailResponse>>> Handle(
        GetFarmLogsByCultivationTaskQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cultivationTask = await _context.CultivationTasks
                .AsNoTracking()
                .Include(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(ct => ct.ProductionPlanTask)
                .FirstOrDefaultAsync(ct => ct.Id == request.CultivationTaskId, cancellationToken);

            if (cultivationTask == null)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Failure(
                    "Cultivation Task not found.",
                    "NotFound");
            }

            var logsQuery = _context.FarmLogs
                .AsNoTracking()
                .Where(fl => fl.CultivationTaskId == request.CultivationTaskId);

            var totalCount = await logsQuery.CountAsync(cancellationToken);

            var pagedLogs = await logsQuery
                .Include(fl => fl.FarmLogMaterials)
                    .ThenInclude(flm => flm.Material)
                .Include(fl => fl.Logger)
                .Include(fl => fl.Verifier)
                .OrderByDescending(fl => fl.LoggedDate)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

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
