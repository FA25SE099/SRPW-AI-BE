using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.SupervisorFeature.Commands.CompletePolygonAssignment;

public class CompletePolygonAssignmentCommandHandler : IRequestHandler<CompletePolygonAssignmentCommand, Result<bool>>
{
    private const int TolerancePercent = 10;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompletePolygonAssignmentCommandHandler> _logger;

    public CompletePolygonAssignmentCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CompletePolygonAssignmentCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(CompletePolygonAssignmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var polygonTask = await _unitOfWork.Repository<PlotPolygonTask>()
                .FindAsync(t => t.Id == request.TaskId);

            if (polygonTask == null)
            {
                return Result<bool>.Failure("Polygon assignment task not found");
            }

            if (polygonTask.AssignedToSupervisorId != request.SupervisorId)
            {
                return Result<bool>.Failure("You are not assigned to this task");
            }

            if (polygonTask.Status == "Completed")
            {
                return Result<bool>.Failure("Task is already completed");
            }

            var targetPlot = await _unitOfWork.Repository<Plot>()
                .FindAsync(p => p.Id == polygonTask.PlotId);

            if (targetPlot == null)
            {
                return Result<bool>.Failure("Plot not found");
            }

            if (!TryParsePolygon(request.PolygonGeoJson, out var polygon, out var parseError))
            {
                return Result<bool>.Failure(parseError!);
            }

            var drawnAreaHa = PlotGeometryHelper.CalculateAreaInHectares(polygon!);
            var plotAreaHa = targetPlot.Area;
            var difference = Math.Abs(drawnAreaHa - plotAreaHa);
            var differencePercent = plotAreaHa > 0 ? (difference / plotAreaHa) * 100 : 0;

            if (differencePercent > TolerancePercent)
            {
                return Result<bool>.Failure(
                    $"Polygon area validation failed. Drawn area ({Math.Round(drawnAreaHa, 2)} ha) differs by {Math.Round(differencePercent, 2)}% from registered plot area ({Math.Round(plotAreaHa, 2)} ha). Maximum allowed difference is {TolerancePercent}%.");
            }

            targetPlot.Boundary = polygon;
            targetPlot.Status = PlotStatus.Active;
            targetPlot.Coordinate = polygon!.Centroid;
            targetPlot.Coordinate.SRID = PlotGeometryHelper.Wgs84Srid;

            _unitOfWork.Repository<Plot>().Update(targetPlot);

            polygonTask.Status = "Completed";
            polygonTask.CompletedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Notes))
            {
                polygonTask.Notes = request.Notes;
            }

            _unitOfWork.Repository<PlotPolygonTask>().Update(polygonTask);

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Supervisor {SupervisorId} completed polygon assignment for Plot {PlotId}",
                request.SupervisorId, polygonTask.PlotId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing polygon assignment for task {TaskId}", request.TaskId);
            return Result<bool>.Failure($"Error completing task: {ex.Message}");
        }
    }

    private bool TryParsePolygon(string polygonGeoJson, out Polygon? polygon, out string? error)
    {
        polygon = null;
        error = null;

        try
        {
            var geometry = new GeoJsonReader().Read<Geometry>(polygonGeoJson);
            polygon = geometry as Polygon;

            if (polygon == null)
            {
                error = "Invalid polygon geometry. Must be a Polygon type.";
                return false;
            }

            polygon.SRID = PlotGeometryHelper.Wgs84Srid;

            if (!polygon.IsValid)
            {
                error = "Invalid polygon geometry. Polygon is not geometrically valid.";
                return false;
            }

            if (polygon.IsEmpty)
            {
                error = "Invalid polygon geometry. Polygon is empty.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing polygon GeoJSON");
            error = $"Invalid GeoJSON format: {ex.Message}";
            return false;
        }
    }
}

