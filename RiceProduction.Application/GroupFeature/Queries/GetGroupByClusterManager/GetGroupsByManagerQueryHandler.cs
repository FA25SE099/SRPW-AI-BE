using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.GroupFeature.Queries.GetGroupByClusterManager;

public class GetGroupsByManagerQueryHandler : IRequestHandler<GetGroupsByManagerQuery, PagedResult<List<ClusterManagerGroupResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetGroupsByManagerQueryHandler> _logger;

    public GetGroupsByManagerQueryHandler(IApplicationDbContext context, ILogger<GetGroupsByManagerQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<ClusterManagerGroupResponse>>> Handle(GetGroupsByManagerQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var managedCluster = await _context.Clusters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClusterManagerId == request.ClusterManagerId, cancellationToken);

            if (managedCluster == null)
            {
                return PagedResult<List<ClusterManagerGroupResponse>>.Failure("No cluster found managed by this user.", "ClusterNotFound");
            }

            var query = _context.Groups
                .AsNoTracking()
                .Where(g => g.ClusterId == managedCluster.Id &&
                            (!request.StatusFilter.HasValue || g.Status == request.StatusFilter.Value));

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedGroups = await query
                .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys.RiceVariety)
                .Include(g => g.Supervisor)
                .Include(g => g.GroupPlots)
                .Include(g => g.ProductionPlans.Where(pp => pp.Status != RiceProduction.Domain.Enums.TaskStatus.Cancelled))
                .AsSplitQuery()
                .OrderBy(g => g.PlantingDate)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var responseData = pagedGroups.Select(g => new ClusterManagerGroupResponse
            {
                GroupId = g.Id,
                Status = g.Status,
                TotalArea = g.TotalArea,
                GroupName = $"{managedCluster.ClusterName} / Group {g.Id.ToString().Substring(0, 4)}",
                PlantingDate = g.PlantingDate,
                RiceVarietyName = g.YearSeason?.RiceVariety?.VarietyName ?? "N/A",
                SupervisorName = g.Supervisor?.FullName ?? "Chưa phân công",
                TotalPlots = g.GroupPlots.Count,
                ActivePlans = g.ProductionPlans.Count
            }).ToList();

            return PagedResult<List<ClusterManagerGroupResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved managed groups."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups managed by Cluster Manager {ManagerId}", request.ClusterManagerId);
            return PagedResult<List<ClusterManagerGroupResponse>>.Failure("Failed to retrieve managed groups.", "GetManagedGroupsFailed");
        }
    }
}