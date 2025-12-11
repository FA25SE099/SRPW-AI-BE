using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByPlotId;

public class GetLateCountByPlotIdQueryHandler : IRequestHandler<GetLateCountByPlotIdQuery, Result<PlotLateCountDTO>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetLateCountByPlotIdQueryHandler> _logger;

    public GetLateCountByPlotIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetLateCountByPlotIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PlotLateCountDTO>> Handle(GetLateCountByPlotIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting late count for plot {PlotId}", request.PlotId);

            var plot = await _unitOfWork.PlotRepository.GetPlotByIDAsync(request.PlotId, cancellationToken);
            if (plot == null)
            {
                return Result<PlotLateCountDTO>.Failure($"Plot with ID {request.PlotId} not found");
            }

            var lateCount = await _unitOfWork.LateFarmerRecordRepository.GetLateCountByPlotIdAsync(request.PlotId, cancellationToken);

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
