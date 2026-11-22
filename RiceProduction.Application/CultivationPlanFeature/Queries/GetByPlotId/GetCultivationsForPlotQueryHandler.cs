using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetByPlotId;

public class GetCultivationsForPlotQueryHandler : IRequestHandler<GetCultivationsForPlotQuery, PagedResult<List<PlotCultivationHistoryResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetCultivationsForPlotQueryHandler> _logger;

    public GetCultivationsForPlotQueryHandler(IApplicationDbContext context, ILogger<GetCultivationsForPlotQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<PlotCultivationHistoryResponse>>> Handle(GetCultivationsForPlotQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plotOwnedByFarmer = await _context.Plots
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.PlotId && p.FarmerId == request.FarmerId, cancellationToken);

            if (!plotOwnedByFarmer)
            {
                _logger.LogWarning("Farmer {FarmerId} attempted to access unauthorized plot {PlotId}", request.FarmerId, request.PlotId);
                return PagedResult<List<PlotCultivationHistoryResponse>>.Failure("Plot not found or unauthorized.", "Unauthorized");
            }

            var baseQuery = _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => pc.PlotId == request.PlotId);

            var totalCount = await baseQuery.CountAsync(cancellationToken);

            var pagedCultivations = await baseQuery
                .Include(pc => pc.Season)
                .Include(pc => pc.RiceVariety)
                .Include(pc => pc.CultivationVersions)
#pragma warning disable CS8602 // Dereference of a possibly null reference
                .Include(pc => pc.CultivationTasks)
                    .ThenInclude(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt.ProductionStage)
                    .ThenInclude(ps => ps.ProductionPlan)
#pragma warning restore CS8602 // Dereference of a possibly null reference
                .AsSplitQuery()
                .OrderByDescending(pc => pc.PlantingDate)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var responseData = pagedCultivations.Select(pc =>
            {
                // Get the latest version (highest VersionOrder) - updated to match GetPlotCultivationByGroupAndPlot pattern
                var latestVersion = pc.CultivationVersions
                    .OrderByDescending(v => v.VersionOrder)
                    .FirstOrDefault();
                
                var planName = pc.CultivationTasks
                    .Where(ct => ct.VersionId == latestVersion?.Id) // Filter Tasks by latest version
                    .Select(ct => ct.ProductionPlanTask?.ProductionStage?.ProductionPlan?.PlanName)
                    .FirstOrDefault(name => name != null);

                return new PlotCultivationHistoryResponse
                {
                    PlotCultivationId = pc.Id,
                    SeasonId = pc.SeasonId,
                    SeasonName = pc.Season.SeasonName, 
                    RiceVarietyId = pc.RiceVarietyId,
                    RiceVarietyName = pc.RiceVariety.VarietyName, 
                    PlantingDate = pc.PlantingDate,
                    Area = pc.Area,
                    Status = pc.Status,
                    ActualYield = pc.ActualYield,
                    ProductionPlanName = planName ?? "Chưa gán",
                    ActiveVersionName = latestVersion?.VersionName ?? "Original"
                };
            }).ToList();

            return PagedResult<List<PlotCultivationHistoryResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                $"Successfully retrieved {responseData.Count} cultivation records for plot {request.PlotId}."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cultivation history for plot {PlotId}", request.PlotId);
            return PagedResult<List<PlotCultivationHistoryResponse>>.Failure($"Failed to retrieve cultivation history: {ex.Message}");
        }
    }
}