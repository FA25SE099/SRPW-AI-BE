using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForClusterManager
{
    public class GetAllSupervisorQueryHandler : IRequestHandler<GetAllSupervisorQuery, PagedResult<List<SupervisorResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _currentUser;

        public GetAllSupervisorQueryHandler(IApplicationDbContext context, IUser currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<List<SupervisorResponse>>> Handle(GetAllSupervisorQuery request, CancellationToken cancellationToken)
        {
            try
            {
                if (!_currentUser.Id.HasValue || _currentUser.Id.Value == Guid.Empty)
                {
                    return PagedResult<List<SupervisorResponse>>.Failure(
                        "Current user ID not found");
                }

                var userId = _currentUser.Id.Value;

                var clusterManager = await _context.ClusterManagers
                    .AsNoTracking()
                    .Where(cm => cm.Id == userId)
                    .Select(cm => new
                    {
                        cm.ClusterId,
                        ClusterName = cm.ManagedCluster != null ? cm.ManagedCluster.ClusterName : null
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (clusterManager == null)
                {
                    return PagedResult<List<SupervisorResponse>>.Failure(
                        $"Cluster Manager with ID {userId} not found");
                }

                if (!clusterManager.ClusterId.HasValue)
                {
                    return PagedResult<List<SupervisorResponse>>.Failure(
                        $"No supervisors found with {userId} cluster id = {clusterManager.ClusterId}");
                }

                var targetClusterId = clusterManager.ClusterId.Value;
                var clusterName = clusterManager.ClusterName ?? "N/A";

                var query = _context.Supervisors
                    .AsNoTracking()
                    .Where(s => s.ClusterId == targetClusterId ||
                                s.SupervisedGroups.Any(g => g.ClusterId == targetClusterId));

                if (!await query.AnyAsync(cancellationToken))
                {
                    return PagedResult<List<SupervisorResponse>>.Failure(
                        $"No supervisors found with {userId} cluster id = {clusterManager.ClusterId}");
                }

                if (!string.IsNullOrWhiteSpace(request.SearchNameOrEmail))
                {
                    var search = request.SearchNameOrEmail.Trim().ToLower();
                    query = query.Where(s =>
                        (s.FullName != null && s.FullName.ToLower().Contains(search)) ||
                        (s.Email != null && s.Email.ToLower().Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(request.SearchPhoneNumber))
                {
                    var phone = request.SearchPhoneNumber.Trim();
                    query = query.Where(s => s.PhoneNumber != null && s.PhoneNumber.Contains(phone));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                var pagedQuery = query
                    .OrderBy(s => s.FullName)
                    .ThenBy(s => s.Id)
                    .AsQueryable();

                if (request.PageSize > 0)
                {
                    pagedQuery = pagedQuery
                        .Skip((request.CurrentPage - 1) * request.PageSize)
                        .Take(request.PageSize);
                }

                var supervisorResponses = await pagedQuery
                    .Select(supervisor => new SupervisorResponse
                    {
                        SupervisorId = supervisor.Id,
                        FullName = supervisor.FullName,
                        Email = supervisor.Email,
                        PhoneNumber = supervisor.PhoneNumber,
                        Address = supervisor.Address,
                        CurrentFarmerCount = supervisor.CurrentFarmerCount,
                        LastActivityAt = supervisor.LastActivityAt,
                        ClusterName = clusterName,
                        ClusterId = supervisor.ClusterId ?? targetClusterId
                    })
                    .ToListAsync(cancellationToken);

                return PagedResult<List<SupervisorResponse>>.Success(
                    supervisorResponses,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount);
            }
            catch (Exception ex)
            {
                return PagedResult<List<SupervisorResponse>>.Failure(
                    $"Error retrieving supervisor list: {ex.Message}");
            }
        }
    }
}
