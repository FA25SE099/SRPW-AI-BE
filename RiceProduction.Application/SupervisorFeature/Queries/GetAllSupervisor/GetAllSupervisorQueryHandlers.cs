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

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisor
{
    public class GetAllSupervisorQueryHandlers : IRequestHandler<GetAllSupervisorQueries, List<SupervisorDTO>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAllSupervisorQueryHandlers> _logger;

        public GetAllSupervisorQueryHandlers(IApplicationDbContext context, ILogger<GetAllSupervisorQueryHandlers> logger)
        {
            _context = context;
            _logger = logger;
        }

        async Task<List<SupervisorDTO>> IRequestHandler<GetAllSupervisorQueries, List<SupervisorDTO>>.Handle(GetAllSupervisorQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving all supervisor...");
                var sup = await _context.Supervisors
                    .AsNoTracking()
                    .Include(s => s.SupervisedGroups).ThenInclude(g => g.ProductionPlans)
                    .Include(s => s.AssignedTasks)
                    .Include(s => s.SupervisorAssignments)
                    .AsSplitQuery()
                    .OrderBy(s => s.FullName)
                    .ToListAsync(cancellationToken);

                if (!sup.Any())
                {
                    _logger.LogInformation("There is no supervisor found");
                }

                var supDTO = sup.Select(s => s.ToDto()).ToList();
                _logger.LogInformation("Retrieved {Count} supervisors", supDTO.Count);
                return supDTO;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supervisor");
                return null;
            }
        }
    }
}
