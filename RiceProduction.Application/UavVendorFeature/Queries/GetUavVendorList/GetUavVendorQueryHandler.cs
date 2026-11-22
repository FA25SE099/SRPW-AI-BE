using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorList
{
    public class GetUavVendorQueryHandler : IRequestHandler<GetUavVendorQuery, PagedResult<List<UavVendorResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetUavVendorQueryHandler> _logger;

        public GetUavVendorQueryHandler(IApplicationDbContext context, ILogger<GetUavVendorQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResult<List<UavVendorResponse>>> Handle(GetUavVendorQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.UavVendors.AsNoTracking();


                var search = request.NameEmailSearch?.ToLower();
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                             (f.Email != null && f.Email.ToLower().Contains(search)));
                }

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(request.PhoneNumber));
                }

                if (!string.IsNullOrEmpty(request.GroupClusterSearch))
                {
                    var groupSearch = request.GroupClusterSearch;
                    query = query.Where(f => f.UavServiceOrders.Any(sv =>
                        sv.GroupId.ToString().ToLower().Contains(groupSearch) ||
                        sv.Group.Cluster.ClusterName.ToLower().Contains(groupSearch) ||
                        sv.Group.Cluster.Id.ToString().ToLower().Contains(groupSearch)));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                var ordered = query.OrderBy(s => s.FullName);
                var paged = request.PageSize == 0
                    ? ordered
                    : ordered.Skip((request.CurrentPage - 1) * request.PageSize).Take(request.PageSize);

                var uavVendorResponse = await paged
                    .Select(uavVendor => new UavVendorResponse
                    {
                        UavVendorId = uavVendor.Id,
                        UavVendorFullName = uavVendor.FullName,
                        VendorName = uavVendor.VendorName,
                        UavVendorPhoneNumber = uavVendor.PhoneNumber,
                        Email = uavVendor.Email
                    })
                    .ToListAsync(cancellationToken);

                return PagedResult<List<UavVendorResponse>>.Success(
                    uavVendorResponse,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Uav Vendors retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving UAV Vendor list");
                throw;
            }
        }
    }
}
