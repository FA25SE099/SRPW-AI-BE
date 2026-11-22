using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetCurrentAgronomyExpert
{
    public class GetCurrentAgronomyExpertQueryHandler : IRequestHandler<GetCurrentAgronomyExpertQuery, Result<CurrentAgronomyExpertResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _currentUser;
        private readonly ILogger<GetCurrentAgronomyExpertQueryHandler> _logger;

        public GetCurrentAgronomyExpertQueryHandler(
            IApplicationDbContext context,
            IUser currentUser,
            ILogger<GetCurrentAgronomyExpertQueryHandler> logger)
        {
            _context = context;
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

                var userId = _currentUser.Id.Value;

                var expert = await _context.AgronomyExperts
                    .AsNoTracking()
                    .Where(e => e.Id == userId)
                    .Select(e => new
                    {
                        e.Id,
                        e.FullName,
                        e.Email,
                        e.ClusterId,
                        ClusterName = e.ManagedCluster != null ? e.ManagedCluster.ClusterName : null,
                        e.AssignedDate
                    })
                    .FirstOrDefaultAsync(cancellationToken);

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
                    ClusterName = expert.ClusterName,
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