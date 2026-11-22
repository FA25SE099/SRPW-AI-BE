using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsByFarmer;

public class GetPlotsByFarmerQueryHandler : IRequestHandler<GetPlotsByFarmerQuery, PagedResult<List<PlotListResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPlotsByFarmerQueryHandler> _logger;

    public GetPlotsByFarmerQueryHandler(
        IApplicationDbContext context,
        ILogger<GetPlotsByFarmerQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<PlotListResponse>>> Handle(
        GetPlotsByFarmerQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var farmerExists = await _context.Farmers
                .AsNoTracking()
                .AnyAsync(f => f.Id == request.FarmerId, cancellationToken);

            if (!farmerExists)
            {
                return PagedResult<List<PlotListResponse>>.Failure(
                    "Farmer not found.",
                    "NotFound");
            }

            var query = _context.Plots
                .AsNoTracking()
                .Where(p => p.FarmerId == request.FarmerId)
                .Include(p => p.GroupPlots)
                    .ThenInclude(gp => gp.Group)
                .Include(p => p.PlotCultivations)
                .Include(p => p.Alerts)
                .AsSplitQuery();

            var totalCount = await query.CountAsync(cancellationToken);

            var plots = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Map to response
            var plotListResponses = plots.Select(p =>
            {
                var latestGroupPlot = p.GroupPlots
                    .OrderByDescending(gp => gp.CreatedAt)
                    .FirstOrDefault();

                var activeCultivations = p.PlotCultivations.Count(pc =>
                    pc.Status == CultivationStatus.Planned || pc.Status == CultivationStatus.InProgress);

                var activeAlerts = p.Alerts.Count(a =>
                    a.Status == AlertStatus.Pending);

                return new PlotListResponse
                {
                    PlotId = p.Id,
                    Area = p.Area,
                    SoThua = p.SoThua,
                    SoTo = p.SoTo,
                    Status = p.Status,
                    GroupId = latestGroupPlot?.GroupId,
                    Boundary = p.Boundary?.AsText(),
                    Coordinate = p.Coordinate?.AsText(),
                    GroupName = latestGroupPlot?.Group?.GroupName,
                    ActiveCultivations = activeCultivations,
                    ActiveAlerts = activeAlerts
                };
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} plots for farmer {FarmerId}",
                plotListResponses.Count,
                request.FarmerId);

            return PagedResult<List<PlotListResponse>>.Success(
                plotListResponses,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved plots.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving plots for farmer {FarmerId}",
                request.FarmerId);
            return PagedResult<List<PlotListResponse>>.Failure(
                "An error occurred while retrieving plots.",
                "GetPlotsFailed");
        }
    }
}
