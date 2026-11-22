using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLatePlotsInCluster;

public class GetLatePlotsInClusterQueryHandler : IRequestHandler<GetLatePlotsInClusterQuery, PagedResult<IEnumerable<PlotWithLateCountDTO>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLatePlotsInClusterQueryHandler> _logger;

    public GetLatePlotsInClusterQueryHandler(IApplicationDbContext context, ILogger<GetLatePlotsInClusterQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<IEnumerable<PlotWithLateCountDTO>>> Handle(GetLatePlotsInClusterQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting plots with late records in cluster");

            List<Guid> plotIds;

            if (request.AgronomyExpertId.HasValue)
            {
                // Get all plots in the cluster that the agronomy expert manages
                var expert = await _context.AgronomyExperts
                    .AsNoTracking()
                    .Where(e => e.Id == request.AgronomyExpertId.Value)
                    .Select(e => new { e.Id, e.ClusterId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (expert == null)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Agronomy Expert not found");
                }

                if (!expert.ClusterId.HasValue)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Agronomy Expert is not assigned to any cluster");
                }

                // Get all late records in the cluster from active versions
                plotIds = await _context.LateFarmerRecords
                    .AsNoTracking()
                    .Where(lr => lr.CultivationTask.PlotCultivation.Plot.GroupPlots
                        .Any(gp => gp.Group.ClusterId == expert.ClusterId.Value)
                        && lr.CultivationTask.Version != null
                        && lr.CultivationTask.Version.IsActive)
                    .Select(lr => lr.CultivationTask.PlotCultivation.PlotId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }
            else if (request.SupervisorId.HasValue)
            {
                // Get plots only from groups that the supervisor is currently managing
                var supervisor = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => s.Id == request.SupervisorId.Value)
                    .Select(s => new { s.Id, s.ClusterId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (supervisor == null)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Supervisor not found");
                }

                if (!supervisor.ClusterId.HasValue)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Supervisor is not assigned to any cluster");
                }

                // Get groups managed by this supervisor
                var groupIds = await _context.Groups
                    .AsNoTracking()
                    .Where(g => g.SupervisorId == request.SupervisorId.Value)
                    .Select(g => g.Id)
                    .ToListAsync(cancellationToken);

                if (groupIds.Count == 0)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Success(
                        data: new List<PlotWithLateCountDTO>(),
                        currentPage: request.PageNumber,
                        pageSize: request.PageSize,
                        totalCount: 0,
                        message: "No groups found for this supervisor"
                    );
                }

                // Get plots with late records in these groups from active versions
                plotIds = await _context.LateFarmerRecords
                    .AsNoTracking()
                    .Where(lr => lr.CultivationTask.PlotCultivation.Plot.GroupPlots
                        .Any(gp => groupIds.Contains(gp.GroupId))
                        && lr.CultivationTask.Version != null
                        && lr.CultivationTask.Version.IsActive)
                    .Select(lr => lr.CultivationTask.PlotCultivation.PlotId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }
            else
            {
                return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Either AgronomyExpertId or SupervisorId must be provided");
            }

            if (plotIds.Count == 0)
            {
                return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Success(
                    data: new List<PlotWithLateCountDTO>(),
                    currentPage: request.PageNumber,
                    pageSize: request.PageSize,
                    totalCount: 0,
                    message: "No plots with late records found"
                );
            }

            // Build plot query
            var plotQuery = _context.Plots
                .AsNoTracking()
                .Where(p => plotIds.Contains(p.Id));

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                plotQuery = plotQuery.Where(p =>
                    (p.Farmer.FullName != null && p.Farmer.FullName.Contains(request.SearchTerm)) ||
                    (p.SoThua.HasValue && p.SoThua.ToString().Contains(request.SearchTerm)) ||
                    (p.SoTo.HasValue && p.SoTo.ToString().Contains(request.SearchTerm)));
            }

            // Get total count
            var totalCount = await plotQuery.CountAsync(cancellationToken);

            // Project paged plots (avoiding PostGIS Boundary/Coordinate columns)
            var plots = await plotQuery
                .OrderBy(p => p.Farmer.FullName)
                .ThenBy(p => p.SoThua)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new
                {
                    p.Id,
                    p.FarmerId,
                    FarmerName = p.Farmer != null ? p.Farmer.FullName : null,
                    GroupId = p.GroupPlots
                        .OrderByDescending(gp => gp.CreatedAt)
                        .Select(gp => (Guid?)gp.GroupId)
                        .FirstOrDefault(),
                    p.SoThua,
                    p.SoTo,
                    p.Area,
                    p.SoilType,
                    p.Status
                })
                .ToListAsync(cancellationToken);

            // Batch fetch late counts for the paged plots
            var pagedPlotIds = plots.Select(p => p.Id).ToList();
            var lateCounts = await _context.LateFarmerRecords
                .AsNoTracking()
                .Where(l => pagedPlotIds.Contains(l.CultivationTask.PlotCultivation.PlotId)
                    && l.CultivationTask.Version != null
                    && l.CultivationTask.Version.IsActive)
                .GroupBy(l => l.CultivationTask.PlotCultivation.PlotId)
                .Select(g => new { PlotId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PlotId, x => x.Count, cancellationToken);

            var result = plots.Select(plot => new PlotWithLateCountDTO
            {
                PlotId = plot.Id,
                FarmerId = plot.FarmerId,
                FarmerName = plot.FarmerName,
                GroupId = plot.GroupId,
                SoThua = plot.SoThua,
                SoTo = plot.SoTo,
                Area = plot.Area,
                SoilType = plot.SoilType,
                Status = plot.Status,
                LateCount = lateCounts.GetValueOrDefault(plot.Id, 0)
            }).ToList();

            return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Success(
                data: result,
                currentPage: request.PageNumber,
                pageSize: request.PageSize,
                totalCount: totalCount,
                message: "Plots with late records retrieved successfully"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting plots with late records in cluster");
            return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("An error occurred while processing your request");
        }
    }
}
