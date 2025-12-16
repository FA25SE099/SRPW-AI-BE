using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByFarmerId;

public class GetLateCountByFarmerIdQueryHandler : IRequestHandler<GetLateCountByFarmerIdQuery, Result<FarmerLateCountDTO>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetLateCountByFarmerIdQueryHandler> _logger;

    public GetLateCountByFarmerIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetLateCountByFarmerIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<FarmerLateCountDTO>> Handle(GetLateCountByFarmerIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting late count for farmer {FarmerId}", request.FarmerId);

            var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.FarmerId, cancellationToken);
            if (farmer == null)
            {
                return Result<FarmerLateCountDTO>.Failure($"Farmer with ID {request.FarmerId} not found");
            }

            var lateCount = await _unitOfWork.LateFarmerRecordRepository.GetLateCountByFarmerIdAsync(request.FarmerId, cancellationToken);

            var result = new FarmerLateCountDTO
            {
                FarmerId = request.FarmerId,
                LateCount = lateCount
            };

            return Result<FarmerLateCountDTO>.Success(result, "Late count retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting late count for farmer {FarmerId}", request.FarmerId);
            return Result<FarmerLateCountDTO>.Failure("An error occurred while processing your request");
        }
    }
}
