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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing polygon GeoJSON");
                return Result<bool>.Failure($"Invalid GeoJSON format: {ex.Message}");
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
}

