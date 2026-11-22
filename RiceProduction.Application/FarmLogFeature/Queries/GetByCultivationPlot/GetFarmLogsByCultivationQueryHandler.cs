using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;

public class GetFarmLogsByCultivationQueryHandler : IRequestHandler<GetFarmLogsByCultivationQuery, PagedResult<List<FarmLogDetailResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetFarmLogsByCultivationQueryHandler> _logger;

    public GetFarmLogsByCultivationQueryHandler(IApplicationDbContext context, ILogger<GetFarmLogsByCultivationQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmLogDetailResponse>>> Handle(GetFarmLogsByCultivationQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plotCultivation = await _context.PlotCultivations
                .AsNoTracking()
                .Include(pc => pc.Plot)
                .FirstOrDefaultAsync(pc => pc.Id == request.PlotCultivationId, cancellationToken);

            if (plotCultivation == null)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Failure("Plot Cultivation not found or unauthorized.", "Unauthorized");
            }

            // Get all CultivationTasks for this PlotCultivation (across all versions)
            var cultivationTaskIds = await _context.CultivationTasks
                .AsNoTracking()
                .Where(ct => ct.PlotCultivationId == request.PlotCultivationId)
                .Select(ct => ct.Id)
                .ToListAsync(cancellationToken);

            if (cultivationTaskIds.Count == 0)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Success(
                    new List<FarmLogDetailResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No cultivation tasks found for this plot cultivation.");
            }

            var logsQuery = _context.FarmLogs
                .AsNoTracking()
                .Where(fl => cultivationTaskIds.Contains(fl.CultivationTaskId));

            var totalCount = await logsQuery.CountAsync(cancellationToken);

            var pagedLogs = await logsQuery
                .Include(fl => fl.CultivationTask)
                    .ThenInclude(ct => ct.ProductionPlanTask)
                .Include(fl => fl.FarmLogMaterials)
                    .ThenInclude(flm => flm.Material)
                .OrderByDescending(fl => fl.LoggedDate)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var plot = plotCultivation.Plot;
            
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
                
                CultivationTaskName = fl.CultivationTask?.CultivationTaskName 
                    ?? fl.CultivationTask?.ProductionPlanTask?.TaskName 
                    ?? "Unknown Task",
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
                "Retrieved {Count} farm logs for Plot Cultivation {PCId} across {TaskCount} cultivation tasks",
                responseData.Count,
                request.PlotCultivationId,
                cultivationTaskIds.Count);

            return PagedResult<List<FarmLogDetailResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved farm log history.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving farm log history for Plot Cultivation {PCId}", request.PlotCultivationId);
            return PagedResult<List<FarmLogDetailResponse>>.Failure("An error occurred while retrieving log history.", "GetFarmLogsFailed");
        }
    }
}