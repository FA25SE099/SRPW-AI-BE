using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetById
{
    public class GetFarmerByIdQueriesHandler : IRequestHandler<GetFarmerByIdQueries ,FarmerDTO>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetFarmerByIdQueriesHandler> _logger;

        public GetFarmerByIdQueriesHandler(IApplicationDbContext context, ILogger<GetFarmerByIdQueriesHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FarmerDTO> Handle(GetFarmerByIdQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving farmer with ID: {FarmerId}", request.Farmerid);
                var farmerDTO = await _context.Farmers
                    .AsNoTracking()
                    .Where(f => f.Id == request.Farmerid)
                    .Select(f => new FarmerDTO
                    {
                        FarmerId = f.Id,
                        FullName = f.FullName,
                        Address = f.Address,
                        PhoneNumber = f.PhoneNumber,
                        IsActive = f.IsActive,
                        IsVerified = f.IsVerified,
                        LastActivityAt = f.LastActivityAt,
                        FarmCode = f.FarmCode,
                        PlotCount = f.OwnedPlots.Count
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (farmerDTO == null)
                {
                    _logger.LogWarning("Farmer with Id:{FarmerId} not found", request.Farmerid);
                    return null;
                }

                _logger.LogInformation(
                    "Successfully retrieve farmer:{FarmerName} (ID: {FarmerId})",
                    farmerDTO.FullName,
                    farmerDTO.FarmerId);
                    
                return farmerDTO;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving farmer: {FarmerId}", request.Farmerid);  
                throw;
            }
        }
    }
}
