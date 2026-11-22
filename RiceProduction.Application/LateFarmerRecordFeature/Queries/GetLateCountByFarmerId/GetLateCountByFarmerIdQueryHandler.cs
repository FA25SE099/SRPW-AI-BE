using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByFarmerId;

public class GetLateCountByFarmerIdQueryHandler : IRequestHandler<GetLateCountByFarmerIdQuery, Result<FarmerLateCountDTO>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLateCountByFarmerIdQueryHandler> _logger;

    public GetLateCountByFarmerIdQueryHandler(IApplicationDbContext context, ILogger<GetLateCountByFarmerIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<FarmerLateCountDTO>> Handle(GetLateCountByFarmerIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting late count for farmer {FarmerId}", request.FarmerId);

            var farmerExists = await _context.Farmers
                .AsNoTracking()
                .AnyAsync(f => f.Id == request.FarmerId, cancellationToken);
            if (!farmerExists)
            {
                return Result<FarmerLateCountDTO>.Failure($"Farmer with ID {request.FarmerId} not found");
            }

            var lateCount = await _context.LateFarmerRecords
                .AsNoTracking()
                .CountAsync(
                    l => l.FarmerId == request.FarmerId
                        && l.CultivationTask.Version != null
                        && l.CultivationTask.Version.IsActive,
                    cancellationToken);

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
