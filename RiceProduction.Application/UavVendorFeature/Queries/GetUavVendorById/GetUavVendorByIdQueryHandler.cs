using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorById
{
    public class GetUavVendorByIdQueryHandler : IRequestHandler<GetUavVendorByIdQuery, Result<UavVendorDetailResponse>>
    {
        private readonly IApplicationDbContext _context;

        public GetUavVendorByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result<UavVendorDetailResponse>> Handle(GetUavVendorByIdQuery request, CancellationToken cancellationToken)
        {
            var response = await _context.UavVendors
                .AsNoTracking()
                .Where(v => v.Id == request.UavVendorId)
                .Select(v => new UavVendorDetailResponse
                {
                    FullName = v.FullName,
                    Email = v.Email,
                    PhoneNumber = v.PhoneNumber,
                    VendorName = v.VendorName,
                    BusinessRegistrationNumber = v.BusinessRegistrationNumber,
                    ServiceRatePerHa = v.ServiceRatePerHa,
                    FleetSize = v.FleetSize,
                    ServiceRadius = v.ServiceRadius,
                    EquipmentSpecs = v.EquipmentSpecs,
                    OperatingSchedule = v.OperatingSchedule
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (response == null)
            {
                return Result<UavVendorDetailResponse>.Failure("UAV Vendor not found");
            }
            return Result<UavVendorDetailResponse>.Success(response);
        }
    }
}
