// GetSupervisorsByClusterIdQueryHandler.cs
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorByClusterId;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorsByClusterId
{
    public class GetSupervisorsByClusterIdQueryHandler
        : IRequestHandler<GetSupervisorByClusterIdQuery, List<SupervisorDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<GetSupervisorsByClusterIdQueryHandler> _logger;

        public GetSupervisorsByClusterIdQueryHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<GetSupervisorsByClusterIdQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }        

        public async Task<List<SupervisorDTO>> Handle(GetSupervisorByClusterIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving supervisors for ClusterId: {ClusterId}", request.ClusterId);

                var supervisors = await _unitOfWork.SupervisorRepository.GetSupervisorsByClusterIdAsync(request.ClusterId);

                if (supervisors == null || !supervisors.Any())
                {
                    _logger.LogWarning("No supervisors found for cluster id: {ClusterId}", request.ClusterId);
                    return new List<SupervisorDTO>();
                }

                var supervisorDTOs = _mapper.Map<List<SupervisorDTO>>(supervisors);

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