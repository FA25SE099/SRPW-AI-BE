using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertList
{
    public class GetAgronomyExpertsQueryHandler : IRequestHandler<GetAgronomyExpertsQuery, PagedResult<List<AgronomyExpertResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAgronomyExpertsQueryHandler> _logger;

        public GetAgronomyExpertsQueryHandler(IApplicationDbContext context, ILogger<GetAgronomyExpertsQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResult<List<AgronomyExpertResponse>>> Handle(GetAgronomyExpertsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.AgronomyExperts.AsNoTracking();

                if (request.FreeOrAssigned.HasValue)
                {
                    query = request.FreeOrAssigned.Value
                        ? query.Where(e => !e.ClusterId.HasValue)
                        : query.Where(e => e.ClusterId.HasValue);
                }

                var search = request.Search?.ToLower();
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(e => (e.FullName != null && e.FullName.ToLower().Contains(search)) ||
                                             (e.Email != null && e.Email.ToLower().Contains(search)));
                }

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    var phoneNumber = request.PhoneNumber;
                    query = query.Where(e => e.PhoneNumber != null && e.PhoneNumber.Contains(phoneNumber));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                var orderedQuery = query.OrderBy(e => e.FullName);
                var pagedQuery = request.PageSize == 0
                    ? orderedQuery
                    : orderedQuery.Skip((request.CurrentPage - 1) * request.PageSize).Take(request.PageSize);

                var agronomyExpertResponse = await pagedQuery
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
                    .ToListAsync(cancellationToken);

                return PagedResult<List<AgronomyExpertResponse>>.Success(
                    agronomyExpertResponse,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Agronomy experts retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting agronomy experts");
                return PagedResult<List<AgronomyExpertResponse>>.Failure("An error occurred while processing your request");
            }
        }
    }
}