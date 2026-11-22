using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorByClusterId;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorsByClusterId
{
    public class GetSupervisorsByClusterIdQueryHandler
        : IRequestHandler<GetSupervisorByClusterIdQuery, List<SupervisorDTO>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetSupervisorsByClusterIdQueryHandler> _logger;

        public GetSupervisorsByClusterIdQueryHandler(
            IApplicationDbContext context,
            ILogger<GetSupervisorsByClusterIdQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<SupervisorDTO>> Handle(GetSupervisorByClusterIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving supervisors for ClusterId: {ClusterId}", request.ClusterId);

                var supervisors = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => s.ClusterId == request.ClusterId)
                    .Include(s => s.SupervisedGroups).ThenInclude(g => g.ProductionPlans)
                    .Include(s => s.AssignedTasks)
                    .Include(s => s.SupervisorAssignments)
                    .AsSplitQuery()
                    .OrderBy(s => s.FullName)
                    .ToListAsync(cancellationToken);

                if (!supervisors.Any())
                {
                    _logger.LogWarning("No supervisors found for cluster id: {ClusterId}", request.ClusterId);
                    return new List<SupervisorDTO>();
                }

                var supervisorDTOs = supervisors.Select(s => s.ToDto()).ToList();

                _logger.LogInformation(
                    "Retrieved {Count} supervisors successfully for cluster {ClusterId}",
                    supervisorDTOs.Count,
                    request.ClusterId
                );

                return supervisorDTOs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supervisors for cluster id: {ClusterId}", request.ClusterId);
                throw;
            }
        }
    }
}
