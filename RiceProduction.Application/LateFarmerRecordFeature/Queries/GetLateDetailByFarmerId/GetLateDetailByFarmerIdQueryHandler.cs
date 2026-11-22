using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateDetailByFarmerId;

public class GetLateDetailByFarmerIdQueryHandler : IRequestHandler<GetLateDetailByFarmerIdQuery, Result<FarmerLateDetailDTO>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLateDetailByFarmerIdQueryHandler> _logger;

    public GetLateDetailByFarmerIdQueryHandler(IApplicationDbContext context, ILogger<GetLateDetailByFarmerIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<FarmerLateDetailDTO>> Handle(GetLateDetailByFarmerIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting late detail for farmer {FarmerId}", request.FarmerId);

            var farmer = await _context.Farmers
                .AsNoTracking()
                .Where(f => f.Id == request.FarmerId)
                .Select(f => new
                {
                    f.Id,
                    f.FullName,
                    f.PhoneNumber,
                    f.Address,
                    f.FarmCode
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (farmer == null)
            {
                return Result<FarmerLateDetailDTO>.Failure($"Farmer with ID {request.FarmerId} not found");
            }

            var lateRecords = await _context.LateFarmerRecords
                .AsNoTracking()
                .Where(l => l.FarmerId == request.FarmerId
                    && l.CultivationTask.Version != null
                    && l.CultivationTask.Version.IsActive)
                .OrderByDescending(l => l.RecordedAt)
                .Select(lr => new
                {
                    lr.Id,
                    lr.FarmerId,
                    FarmerName = lr.Farmer != null ? lr.Farmer.FullName : null,
                    lr.CultivationTaskId,
                    TaskName = lr.CultivationTask.CultivationTaskName,
                    lr.CultivationTask.PlotCultivationId,
                    PlotId = lr.CultivationTask.PlotCultivation.PlotId,
                    SoThua = lr.CultivationTask.PlotCultivation.Plot.SoThua,
                    SoTo = lr.CultivationTask.PlotCultivation.Plot.SoTo,
                    SeasonId = lr.CultivationTask.PlotCultivation.SeasonId,
                    SeasonName = lr.CultivationTask.PlotCultivation.Season.SeasonName,
                    GroupPlots = lr.CultivationTask.PlotCultivation.Plot.GroupPlots
                        .Select(gp => new
                        {
                            gp.CreatedAt,
                            gp.GroupId,
                            gp.Group.GroupName,
                            gp.Group.ClusterId,
                            ClusterName = gp.Group.Cluster != null ? gp.Group.Cluster.ClusterName : null
                        })
                        .ToList(),
                    lr.RecordedAt,
                    lr.Notes
                })
                .ToListAsync(cancellationToken);

            var lateRecordDTOs = lateRecords.Select(lr =>
            {
                var latestGroup = lr.GroupPlots
                    .OrderByDescending(gp => gp.CreatedAt)
                    .FirstOrDefault();

                return new LateFarmerRecordDTO
                {
                    Id = lr.Id,
                    FarmerId = lr.FarmerId,
                    FarmerName = lr.FarmerName,
                    TaskId = lr.CultivationTaskId,
                    TaskName = lr.TaskName,
                    PlotId = lr.PlotId,
                    SoThua = lr.SoThua,
                    SoTo = lr.SoTo,
                    PlotCultivationId = lr.PlotCultivationId,
                    SeasonId = lr.SeasonId,
                    SeasonName = lr.SeasonName,
                    GroupId = latestGroup?.GroupId ?? Guid.Empty,
                    GroupName = latestGroup?.GroupName,
                    ClusterId = latestGroup?.ClusterId ?? Guid.Empty,
                    ClusterName = latestGroup?.ClusterName,
                    RecordedAt = lr.RecordedAt,
                    Notes = lr.Notes
                };
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
