using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertById
{
    public class GetAgronomyExpertByIdQueryHandler : IRequestHandler<GetAgronomyExpertByIdQuery, Result<AgronomyExpertResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAgronomyExpertByIdQueryHandler> _logger;

        public GetAgronomyExpertByIdQueryHandler(IApplicationDbContext context, ILogger<GetAgronomyExpertByIdQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<AgronomyExpertResponse>> Handle(GetAgronomyExpertByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var agronomyExpertResponse = await _context.AgronomyExperts
                    .AsNoTracking()
                    .Where(e => e.Id == request.AgronomyExpertId)
                    .Select(e => new AgronomyExpertResponse
                    {
                        ExpertId = e.Id,
                        ExpertName = e.FullName,
                        ExpertPhoneNumber = e.PhoneNumber,
                        Email = e.Email,
                        ClusterId = e.ClusterId,
                        ClusterName = e.ManagedCluster != null ? e.ManagedCluster.ClusterName : null,
                        AssignedDate = e.AssignedDate
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (agronomyExpertResponse == null)
                {
                    return Result<AgronomyExpertResponse>.Failure("Agronomy expert not found");
                }

                return Result<AgronomyExpertResponse>.Success(
                    agronomyExpertResponse,
                    "Agronomy expert retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting agronomy expert");
                return Result<AgronomyExpertResponse>.Failure("An error occurred while processing your request");
            }
        }
    }
}