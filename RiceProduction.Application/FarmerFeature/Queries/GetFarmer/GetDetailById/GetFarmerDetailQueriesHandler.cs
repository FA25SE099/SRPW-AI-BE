using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetDetailById
{
    public class GetFarmerDetailQueriesHandler : IRequestHandler<GetFarmerDetailQueries, FarmerDetailDTO>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetFarmerDetailQueriesHandler> _logger;

        public GetFarmerDetailQueriesHandler(IApplicationDbContext context, ILogger<GetFarmerDetailQueriesHandler> logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task<FarmerDetailDTO> Handle(GetFarmerDetailQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving detail farmer data with ID: {FarmerId}", request.FarmerId);

                // Identity resolution keeps shared Group instances identical so Distinct() in ToDetailDto still works without tracking
                var farmer = await _context.Farmers
                    .AsNoTrackingWithIdentityResolution()
                    .AsSplitQuery()
                    .Include(f => f.OwnedPlots)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
                                .ThenInclude(g => g.ProductionPlans)
                                    .ThenInclude(pp => pp.CurrentProductionStages)
                                        .ThenInclude(ps => ps.ProductionPlanTasks)
                    .Include(f => f.OwnedPlots).ThenInclude(p => p.GroupPlots).ThenInclude(gp => gp.Group).ThenInclude(g => g.YearSeason).ThenInclude(ys => ys.RiceVariety)
                    .Include(f => f.OwnedPlots).ThenInclude(p => p.GroupPlots).ThenInclude(gp => gp.Group).ThenInclude(c => c.Cluster)
                    .Include(f => f.OwnedPlots).ThenInclude(p => p.GroupPlots).ThenInclude(gp => gp.Group).ThenInclude(s => s.Supervisor)
                    .Include(f => f.FarmerAssignments).ThenInclude(a => a.Supervisor)
                    .FirstOrDefaultAsync(f => f.Id == request.FarmerId, cancellationToken);

                if (farmer == null)
                {
                    _logger.LogWarning("Farmer with Id:{FarmerId} not found", request.FarmerId);
                    return null;
                }
                var farmerDTO = farmer.ToDetailDto();

                _logger.LogInformation(
                   "Successfully retrieve farmer:{FarmerName} (ID: {FarmerId})",
                   farmer.FullName,
                   farmer.Id);

                return farmerDTO;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving farmer: {FarmerId}", request.FarmerId);
                throw;
            }
        }
    }
}
