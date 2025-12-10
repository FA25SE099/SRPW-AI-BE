using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateDetailByFarmerId;

public class GetLateDetailByFarmerIdQueryHandler : IRequestHandler<GetLateDetailByFarmerIdQuery, Result<FarmerLateDetailDTO>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetLateDetailByFarmerIdQueryHandler> _logger;

    public GetLateDetailByFarmerIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ILogger<GetLateDetailByFarmerIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<FarmerLateDetailDTO>> Handle(GetLateDetailByFarmerIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting late detail for farmer {FarmerId}", request.FarmerId);

            var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.FarmerId, cancellationToken);
            if (farmer == null)
            {
                return Result<FarmerLateDetailDTO>.Failure($"Farmer with ID {request.FarmerId} not found");
            }

            var lateRecords = await _unitOfWork.LateFarmerRecordRepository.GetLateRecordsByFarmerIdAsync(request.FarmerId, cancellationToken);

            var lateRecordDTOs = lateRecords.Select(lr => new LateFarmerRecordDTO
            {
                Id = lr.Id,
                FarmerId = lr.FarmerId,
                FarmerName = lr.Farmer?.FullName,
                TaskId = lr.TaskId,
                TaskName = lr.Task?.CultivationTaskName,
                PlotId = lr.PlotId,
                SoThua = lr.Plot?.SoThua,
                SoTo = lr.Plot?.SoTo,
                PlotCultivationId = lr.PlotCultivationId,
                SeasonId = lr.SeasonId,
                SeasonName = lr.Season?.SeasonName,
                GroupId = lr.GroupId,
                GroupName = lr.Group?.GroupName,
                ClusterId = lr.ClusterId,
                ClusterName = lr.Cluster?.ClusterName,
                RecordedAt = lr.RecordedAt,
                Notes = lr.Notes
            }).ToList();

            var result = new FarmerLateDetailDTO
            {
                FarmerId = farmer.Id,
                FullName = farmer.FullName,
                PhoneNumber = farmer.PhoneNumber,
                Address = farmer.Address,
                FarmCode = farmer.FarmCode,
                TotalLateCount = lateRecordDTOs.Count,
                LateRecords = lateRecordDTOs
            };

            return Result<FarmerLateDetailDTO>.Success(result, "Late detail retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting late detail for farmer {FarmerId}", request.FarmerId);
            return Result<FarmerLateDetailDTO>.Failure("An error occurred while processing your request");
        }
    }
}
