using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupResponses;

namespace RiceProduction.Application.GroupFeature.Queries.GetGroupsByClusterId
{
    public class GetGroupsByClusterManagerIdQueryHandler : IRequestHandler<GetGroupsByClusterManagerIdQuery, PagedResult<List<GroupResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly WKTWriter _wktWriter;
        private readonly IUser _currentUser;
        private readonly ILogger<GetGroupsByClusterManagerIdQueryHandler> _logger;

        public GetGroupsByClusterManagerIdQueryHandler(
            IApplicationDbContext context,
            IUser currentUser,
            ILogger<GetGroupsByClusterManagerIdQueryHandler> logger)
        {
            _context = context;
            _wktWriter = new WKTWriter();
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task<PagedResult<List<GroupResponse>>> Handle(GetGroupsByClusterManagerIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                if (!_currentUser.Id.HasValue || _currentUser.Id.Value == Guid.Empty)
                {
                    return PagedResult<List<GroupResponse>>.Failure(
                        "Current user ID not found");
                }

                var userId = _currentUser.Id.Value;

                var clusterManager = await _context.ClusterManagers
                    .AsNoTracking()
                    .Where(m => m.Id == userId)
                    .Select(m => new { m.ClusterId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (clusterManager == null)
                {
                    return PagedResult<List<GroupResponse>>.Failure(
                        $"Cluster Manager with ID {userId} not found");
                }

                if (!clusterManager.ClusterId.HasValue)
                {
                    return PagedResult<List<GroupResponse>>.Success(
                        new List<GroupResponse>(),
                        request.CurrentPage,
                        request.PageSize,
                        0);
                }

                var clusterId = clusterManager.ClusterId.Value;
                var groupsQuery = _context.Groups
                    .AsNoTracking()
                    .Where(g => g.ClusterId == clusterId);

                var totalCount = await groupsQuery.CountAsync(cancellationToken);

                var groups = await groupsQuery
                    .OrderByDescending(g => g.CreatedAt)
                    .Skip((request.CurrentPage - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(g => new
                    {
                        g.ClusterId,
                        g.SupervisorId,
                        SupervisorName = g.Supervisor != null ? g.Supervisor.FullName : null,
                        RiceVarietyId = g.YearSeason != null ? (Guid?)g.YearSeason.RiceVarietyId : null,
                        SeasonId = g.YearSeason != null ? (Guid?)g.YearSeason.SeasonId : null,
                        g.PlantingDate,
                        g.Status,
                        g.IsException,
                        g.ExceptionReason,
                        g.ReadyForUavDate,
                        g.Area,
                        g.TotalArea
                    })
                    .ToListAsync(cancellationToken);

                var groupResponses = groups.Select(g => new GroupResponse
                {
                    ClusterId = g.ClusterId,
                    SupervisorId = g.SupervisorId,
                    SupervisorName = g.SupervisorName ?? "No name",
                    RiceVarietyId = g.RiceVarietyId,
                    SeasonId = g.SeasonId,
                    PlantingDate = g.PlantingDate,
                    Status = g.Status,
                    IsException = g.IsException,
                    ExceptionReason = g.ExceptionReason,
                    ReadyForUavDate = g.ReadyForUavDate,
                    Area = g.Area != null ? _wktWriter.Write(g.Area) : null,
                    TotalArea = g.TotalArea
                }).ToList();

                return PagedResult<List<GroupResponse>>.Success(
                    groupResponses,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving groups for cluster manager");
                return PagedResult<List<GroupResponse>>.Failure($"An error occurred while retrieving groups: {ex.Message}");
            }
        }
    }
}