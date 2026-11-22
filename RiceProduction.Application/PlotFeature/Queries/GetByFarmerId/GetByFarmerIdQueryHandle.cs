using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
public class GetByFarmerIdQueryHandler : IRequestHandler<GetByFarmerIdQuery, PagedResult<List<PlotListResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetByFarmerIdQueryHandler> _logger;

    public GetByFarmerIdQueryHandler(IApplicationDbContext context, ILogger<GetByFarmerIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<PlotListResponse>>> Handle(GetByFarmerIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.Plots
                .AsNoTracking()
                .Where(p => p.FarmerId == request.FarmerId);

            if (request.Status.HasValue)
            {
                var status = request.Status.Value;
                query = query.Where(p => p.Status == status);
            }

            if (request.IsUnassigned.HasValue)
            {
                query = request.IsUnassigned.Value
                    ? query.Where(p => !p.GroupPlots.Any())   // thửa CHƯA gán
                    : query.Where(p => p.GroupPlots.Any());   // thửa ĐÃ gán
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedPlots = await query
                .OrderBy(p => p.SoTo).ThenBy(p => p.SoThua).ThenBy(p => p.Id)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Area,
                    p.SoThua,
                    p.SoTo,
                    p.Status,
                    p.Boundary,
                    p.Coordinate,
                    FirstGroup = p.GroupPlots
                        .Select(gp => new { gp.GroupId, ClusterName = gp.Group.Cluster.ClusterName })
                        .FirstOrDefault(),
                    ActiveAlerts = p.Alerts.Count(a => a.Status == AlertStatus.Pending || a.Status == AlertStatus.UnderReview),
                    ActiveCultivations = p.PlotCultivations.Count(pc => pc.Status == CultivationStatus.Planned || pc.Status == CultivationStatus.InProgress)
                })
                .ToListAsync(cancellationToken);

            var responseData = pagedPlots.Select(p => new PlotListResponse
            {
                PlotId = p.Id,
                Area = p.Area,
                SoThua = p.SoThua,
                SoTo = p.SoTo,
                Status = p.Status,
                Boundary = p.Boundary != null ? p.Boundary.AsText() : null,
                Coordinate = p.Coordinate != null ? p.Coordinate.AsText() : null,
                GroupId = p.FirstGroup?.GroupId,
                GroupName = p.FirstGroup?.ClusterName, // Lấy tên Cluster làm tên Group
                ActiveAlerts = p.ActiveAlerts,
                ActiveCultivations = p.ActiveCultivations
            }).ToList();
            return PagedResult<List<PlotListResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                $"Successfully retrieved {responseData.Count} plots for farmer {request.FarmerId}."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving plots for farmer {FarmerId}", request.FarmerId);
            return PagedResult<List<PlotListResponse>>.Failure($"Failed to retrieve plots: {ex.Message}");
        }
    }
}