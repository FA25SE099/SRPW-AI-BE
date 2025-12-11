using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.CheckPlotPolygonEditable;

public class CheckPlotPolygonEditableQueryHandler : IRequestHandler<CheckPlotPolygonEditableQuery, Result<CheckPlotPolygonEditableResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CheckPlotPolygonEditableQueryHandler> _logger;

    public CheckPlotPolygonEditableQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<CheckPlotPolygonEditableQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CheckPlotPolygonEditableResponse>> Handle(CheckPlotPolygonEditableQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plotExists = await _unitOfWork.PlotRepository.ExistPlotAsync(request.PlotId, cancellationToken);
            if (!plotExists)
            {
                return Result<CheckPlotPolygonEditableResponse>.Failure("Plot not found");
            }

            var isInGroup = await _unitOfWork.PlotRepository.IsPlotAssignedToGroupForYearSeasonAsync(
                request.PlotId, 
                request.YearSeasonId, 
                cancellationToken);

            if (isInGroup)
            {
                var response = new CheckPlotPolygonEditableResponse
                {
                    IsEditable = false,
                    Reason = "Cannot edit plot polygon. This plot is already assigned to a group in this season."
                };
                return Result<CheckPlotPolygonEditableResponse>.Success(response);
            }

            var editableResponse = new CheckPlotPolygonEditableResponse
            {
                IsEditable = true,
                Reason = null
            };

            return Result<CheckPlotPolygonEditableResponse>.Success(editableResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if plot polygon is editable for PlotId: {PlotId}, YearSeasonId: {YearSeasonId}", 
                request.PlotId, request.YearSeasonId);
            return Result<CheckPlotPolygonEditableResponse>.Failure("Failed to check plot polygon editability");
        }
    }
}

