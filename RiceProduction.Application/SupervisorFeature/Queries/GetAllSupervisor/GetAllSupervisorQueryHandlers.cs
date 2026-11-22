using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForAdmin;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisor
{
    public class GetAllSupervisorQueryHandlers : IRequestHandler<GetAllSupervisorQueries, List<SupervisorDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAllSupervisorQueryHandlers> _logger;

        public GetAllSupervisorQueryHandlers(IUnitOfWork unitOfWork, ILogger<GetAllSupervisorQueryHandlers> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        
        async Task<List<SupervisorDTO>> IRequestHandler<GetAllSupervisorQueries, List<SupervisorDTO>>.Handle(GetAllSupervisorQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving all supervisor...");
                var sup = await _unitOfWork.SupervisorRepository.GetAllSupervisorAsync(cancellationToken);
                if (sup == null)
                {
                    _logger.LogInformation("There is no supervisor found");
                }
                var supDTO = sup?.Where(s => s != null).Select(s => s!.ToDto()).ToList() ?? new List<SupervisorDTO>();
                _logger.LogInformation($"{supDTO}");
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
