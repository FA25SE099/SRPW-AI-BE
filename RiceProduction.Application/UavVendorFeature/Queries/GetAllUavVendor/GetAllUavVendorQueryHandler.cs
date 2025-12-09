using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetAllUavVendor;

public class GetAllUavVendorQueryHandler : IRequestHandler<GetAllUavVendorQuery, Result<List<UavVendorDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllUavVendorQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<UavVendorDto>>> Handle(GetAllUavVendorQuery request, CancellationToken cancellationToken)
    {
        var vendors = await _unitOfWork.UavVendorRepository.GetAllUavVendorAsync();

        var vendorResponses = vendors.Select(v => new UavVendorDto
        {
            Id = v.Id,
            Name = v.FullName,
            Address = v.Address,
            Email = v.Email
        }).ToList();

        return Result<List<UavVendorDto>>.Success(vendorResponses);
    }
}
