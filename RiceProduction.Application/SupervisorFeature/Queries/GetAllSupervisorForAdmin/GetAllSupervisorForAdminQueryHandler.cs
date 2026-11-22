using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForAdmin
{
    public class GetAllSupervisorForAdminQueryHandler : IRequestHandler<GetAllSupervisorForAdminQuery, PagedResult<List<SupervisorResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAllSupervisorForAdminQueryHandler> _logger;

        public GetAllSupervisorForAdminQueryHandler(IApplicationDbContext context, ILogger<GetAllSupervisorForAdminQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResult<List<SupervisorResponse>>> Handle(GetAllSupervisorForAdminQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Supervisors.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(request.SearchNameOrEmail))
                {
                    var search = request.SearchNameOrEmail.Trim().ToLower();
                    query = query.Where(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                             (f.Email != null && f.Email.ToLower().Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(request.SearchPhoneNumber))
                {
                    var phoneNumber = request.SearchPhoneNumber.Trim();
                    query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber));
                }

                if (!string.IsNullOrWhiteSpace(request.AdvancedSearch))
                {
                    var groupSearch = request.AdvancedSearch.Trim().ToLower();
                    query = query.Where(f =>
                        (f.SupervisedGroups.Any(sv => sv.Id.ToString().ToLower().Contains(groupSearch))
                            || f.SupervisedGroups.Any(sv => sv.Cluster != null && sv.Cluster.ClusterName.ToLower().Contains(groupSearch))
                            || f.SupervisedGroups.Any(sv => sv.ClusterId.ToString().ToLower().Contains(groupSearch)))
                        ||
                        (f.AssignedTasks.Any(sv => sv.PlotCultivation != null && sv.PlotCultivation.PlotId.ToString().ToLower().Contains(groupSearch))
                            || f.AssignedTasks.Any(sv => sv.PlotCultivation != null && sv.PlotCultivation.Plot.GroupPlots.Any(gp => gp.GroupId.ToString().ToLower().Contains(groupSearch))))
                        ||
                        (f.SupervisorAssignments.Any(sv => sv.FarmerId.ToString().ToLower().Contains(groupSearch))
                            || f.SupervisorAssignments.Any(sv => sv.Farmer != null && sv.Farmer.FullName != null && sv.Farmer.FullName.ToLower().Contains(groupSearch))
                            || f.SupervisorAssignments.Any(sv => sv.Farmer != null && sv.Farmer.Email != null && sv.Farmer.Email.ToLower().Contains(groupSearch))
                            || f.SupervisorAssignments.Any(sv => sv.Farmer != null && sv.Farmer.PhoneNumber != null && sv.Farmer.PhoneNumber.Contains(groupSearch)))
                    );
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
                        Address = supervisor.Address,
                        CurrentFarmerCount = supervisor.CurrentFarmerCount,
                        Email = supervisor.Email,
                        FullName = supervisor.FullName,
                        LastActivityAt = supervisor.LastActivityAt,
                        PhoneNumber = supervisor.PhoneNumber,
                        ClusterName = supervisor.ManagedCluster != null ? supervisor.ManagedCluster.ClusterName : "N/A",
                        ClusterId = supervisor.ManagedCluster != null ? (Guid?)supervisor.ManagedCluster.Id : null
                    })
                    .ToListAsync(cancellationToken);

                return PagedResult<List<SupervisorResponse>>.Success(
                    supervisorResponses,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Supervisors retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Supervisor list");
                throw;
            }
        }
    }
}
