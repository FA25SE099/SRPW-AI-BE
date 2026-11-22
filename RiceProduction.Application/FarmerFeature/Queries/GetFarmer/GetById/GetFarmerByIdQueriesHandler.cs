using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetById
{
    public class GetFarmerByIdQueriesHandler : IRequestHandler<GetFarmerByIdQueries ,FarmerDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetFarmerByIdQueriesHandler> _logger;

        public GetFarmerByIdQueriesHandler(IUnitOfWork unitOfWork, ILogger<GetFarmerByIdQueriesHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<FarmerDTO> Handle(GetFarmerByIdQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving farmer with ID: {FarmerId}", request.Farmerid);
                var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.Farmerid, cancellationToken);
                if (farmer == null)
                {
                    _logger.LogWarning("Farmer with Id:{FarmerId} not found", request.Farmerid);
                    return null;
                }
                var farmerDTO = farmer.ToDto();

                _logger.LogInformation(
                    "Successfully retrieve farmer:{FarmerName} (ID: {FarmerId})",
                    farmer.FullName,
                    farmer.Id);
                    
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
