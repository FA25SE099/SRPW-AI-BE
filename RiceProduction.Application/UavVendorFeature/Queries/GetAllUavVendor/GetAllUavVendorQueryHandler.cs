using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetAllUavVendor;

public class GetAllUavVendorQueryHandler : IRequestHandler<GetAllUavVendorQuery, Result<List<UavVendorDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllUavVendorQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<UavVendorDto>>> Handle(GetAllUavVendorQuery request, CancellationToken cancellationToken)
    {
        var vendorResponses = await _context.UavVendors
            .AsNoTracking()
            .Select(v => new UavVendorDto
            {
                Id = v.Id,
                Name = v.FullName,
                Address = v.Address,
                Email = v.Email
            })
            .ToListAsync(cancellationToken);

        return Result<List<UavVendorDto>>.Success(vendorResponses);
    }
}
