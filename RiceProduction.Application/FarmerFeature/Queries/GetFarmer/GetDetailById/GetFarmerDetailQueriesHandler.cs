using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetDetailById
{
    public class GetFarmerDetailQueriesHandler : IRequestHandler<GetFarmerDetailQueries, FarmerDetailDTO>
    {
        private IUnitOfWork _unitOfWork;
        private IMapper _mapper;
        private ILogger<GetFarmerDetailQueriesHandler> _logger;

        public GetFarmerDetailQueriesHandler(IUnitOfWork unitOfWork, IMapper mapper, ILogger<GetFarmerDetailQueriesHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }
        public async Task<FarmerDetailDTO> Handle(GetFarmerDetailQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving detail farmer data with ID: {FarmerId}", request.FarmerId);
                var farmer = await _unitOfWork.FarmerRepository.GetFarmerDetailByIdAsync(request.FarmerId, cancellationToken);
                if (farmer == null)
                {
                    _logger.LogWarning("Farmer with Id:{FarmerId} not found", request.FarmerId);
                    return null;
                }
                var farmerDTO = _mapper.Map<FarmerDetailDTO>(farmer);

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
