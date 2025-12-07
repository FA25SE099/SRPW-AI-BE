using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.SupervisorFeature.Commands.CompletePolygonAssignment;

public class CompletePolygonAssignmentCommandHandler : IRequestHandler<CompletePolygonAssignmentCommand, Result<bool>>
{
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
            // Get the task
            var polygonTask = await _unitOfWork.Repository<PlotPolygonTask>()
                .FindAsync(t => t.Id == request.TaskId);
            
            if (polygonTask == null)
            {
                return Result<bool>.Failure("Polygon assignment task not found");
            }

            // Verify supervisor is assigned to this task
            if (polygonTask.AssignedToSupervisorId != request.SupervisorId)
            {
                return Result<bool>.Failure("You are not assigned to this task");
            }

            // Verify task is not already completed
            if (polygonTask.Status == "Completed")
            {
                return Result<bool>.Failure("Task is already completed");
            }

            // Get the plot
            var targetPlot = await _unitOfWork.Repository<Plot>()
                .FindAsync(p => p.Id == polygonTask.PlotId);
            
            if (targetPlot == null)
            {
                return Result<bool>.Failure("Plot not found");
            }

            // Parse GeoJSON to Polygon geometry
            Polygon? polygon;
            try
            {
                var geoJsonReader = new GeoJsonReader();
                var geometry = geoJsonReader.Read<Geometry>(request.PolygonGeoJson);
                
                polygon = geometry as Polygon;
                if (polygon == null)
                {
                    return Result<bool>.Failure("Invalid polygon geometry. Must be a Polygon type.");
                }

                // Set SRID to 4326 (WGS84)
                polygon.SRID = 4326;

                // Validate polygon geometry
                if (!polygon.IsValid)
                {
                    return Result<bool>.Failure("Invalid polygon geometry. Polygon is not geometrically valid.");
                }

                if (polygon.IsEmpty)
                {
                    return Result<bool>.Failure("Invalid polygon geometry. Polygon is empty.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing polygon GeoJSON");
                return Result<bool>.Failure($"Invalid GeoJSON format: {ex.Message}");
            }

            // Server-side area validation with 10% tolerance
            var drawnAreaHa = CalculateAreaInHectares(polygon);
            var plotAreaHa = targetPlot.Area;
            var difference = Math.Abs(drawnAreaHa - plotAreaHa);
            var differencePercent = plotAreaHa > 0 ? (difference / plotAreaHa) * 100 : 0;
            var tolerancePercent = 10; // 10% tolerance

            if (differencePercent > tolerancePercent)
            {
                return Result<bool>.Failure(
                    $"Polygon area validation failed. Drawn area ({Math.Round(drawnAreaHa, 2)} ha) differs by {Math.Round(differencePercent, 2)}% from registered plot area ({Math.Round(plotAreaHa, 2)} ha). Maximum allowed difference is {tolerancePercent}%.");
            }

            // Update the plot with the polygon boundary
            targetPlot.Boundary = polygon;
            targetPlot.Status = PlotStatus.Active;
            
            // Calculate and update centroid
            if (polygon.IsValid && !polygon.IsEmpty)
            {
                targetPlot.Coordinate = polygon.Centroid;
                targetPlot.Coordinate.SRID = 4326;
            }

            _unitOfWork.Repository<Plot>().Update(targetPlot);

            // Update the task
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

    /// <summary>
    /// Calculate area in hectares from a polygon with SRID 4326 (WGS84)
    /// </summary>
    private decimal CalculateAreaInHectares(Polygon polygon)
    {
        // For WGS84 (SRID 4326), the area is in square degrees
        // We need to convert to square meters, then to hectares
        // Using approximate conversion: 1 degree ≈ 111,319.9 meters at equator
        var areaInSquareDegrees = polygon.Area;
        
        // Convert to square meters (approximation for small areas)
        var areaInSquareMeters = areaInSquareDegrees * 111319.9 * 111319.9;
        
        // Convert to hectares (1 hectare = 10,000 square meters)
        var areaInHectares = areaInSquareMeters / 10000;

        return (decimal)areaInHectares;
    }
}

