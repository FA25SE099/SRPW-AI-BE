using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByPlotId;

public class GetLateCountByPlotIdQueryHandler : IRequestHandler<GetLateCountByPlotIdQuery, Result<PlotLateCountDTO>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLateCountByPlotIdQueryHandler> _logger;

    public GetLateCountByPlotIdQueryHandler(IApplicationDbContext context, ILogger<GetLateCountByPlotIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<PlotLateCountDTO>> Handle(GetLateCountByPlotIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting late count for plot {PlotId}", request.PlotId);

            var plotExists = await _context.Plots
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.PlotId, cancellationToken);
            if (!plotExists)
            {
                return Result<PlotLateCountDTO>.Failure($"Plot with ID {request.PlotId} not found");
            }

            var lateCount = await _context.LateFarmerRecords
                .AsNoTracking()
                .CountAsync(
                    l => l.CultivationTask.PlotCultivation.PlotId == request.PlotId
                        && l.CultivationTask.Version != null
                        && l.CultivationTask.Version.IsActive,
                    cancellationToken);

            var result = new PlotLateCountDTO
            {
                PlotId = request.PlotId,
                LateCount = lateCount
            };

            return Result<PlotLateCountDTO>.Success(result, "Late count retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting late count for plot {PlotId}", request.PlotId);
            return Result<PlotLateCountDTO>.Failure("An error occurred while processing your request");
        }
    }
}
