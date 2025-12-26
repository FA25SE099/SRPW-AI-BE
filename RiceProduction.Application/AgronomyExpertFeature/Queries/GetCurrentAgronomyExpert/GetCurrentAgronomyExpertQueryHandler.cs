using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetCurrentAgronomyExpert
{
    public class GetCurrentAgronomyExpertQueryHandler : IRequestHandler<GetCurrentAgronomyExpertQuery, Result<CurrentAgronomyExpertResponse>>
    {
        private readonly IAgronomyExpertRepository _expertRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUser _currentUser;
        private readonly ILogger<GetCurrentAgronomyExpertQueryHandler> _logger;

        public GetCurrentAgronomyExpertQueryHandler(
            IUnitOfWork unitOfWork, 
            IUser currentUser,
            ILogger<GetCurrentAgronomyExpertQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _expertRepo = _unitOfWork.AgronomyExpertRepository;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task<Result<CurrentAgronomyExpertResponse>> Handle(GetCurrentAgronomyExpertQuery request, CancellationToken cancellationToken)
        {
            try
            {
                if (_currentUser.Id == null)
                {
                    return Result<CurrentAgronomyExpertResponse>.Failure("User not authenticated");
                }

                var expert = await _expertRepo.GetAgronomyExpertByIdAsync(
                    _currentUser.Id.Value,
                    cancellationToken);

                if (expert == null)
                {
                    return Result<CurrentAgronomyExpertResponse>.Failure("Agronomy expert not found");
                }

                var agronomyExpertResponse = new CurrentAgronomyExpertResponse
                {
                    ExpertId = expert.Id.ToString(),
                    ExpertName = expert.FullName,
                    Email = expert.Email,
                    ClusterId = expert.ClusterId?.ToString(),
                    ClusterName = expert.ManagedCluster?.ClusterName,
                    AssignedDate = expert.AssignedDate?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                return Result<CurrentAgronomyExpertResponse>.Success(
                    agronomyExpertResponse,
                    "Current agronomy expert retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting current agronomy expert");
                return Result<CurrentAgronomyExpertResponse>.Failure("An error occurred while processing your request");
            }
        }
    }
}

