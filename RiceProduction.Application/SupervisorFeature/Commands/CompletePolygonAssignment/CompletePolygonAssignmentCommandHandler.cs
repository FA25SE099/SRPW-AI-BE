using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Commands.CompletePolygonAssignment;

public class CompletePolygonAssignmentCommandHandler : IRequestHandler<CompletePolygonAssignmentCommand, Result<bool>>
{
    private const int TolerancePercent = 10;

    private readonly IApplicationDbContext _context;
    private readonly ILogger<CompletePolygonAssignmentCommandHandler> _logger;

    public CompletePolygonAssignmentCommandHandler(
        IApplicationDbContext context,
        ILogger<CompletePolygonAssignmentCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(CompletePolygonAssignmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var polygonTask = await _context.PlotPolygonTasks
                .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

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

            var targetPlot = await _context.Plots
                .FirstOrDefaultAsync(p => p.Id == polygonTask.PlotId, cancellationToken);

            if (targetPlot == null)
            {
                return Result<bool>.Failure("Plot not found");
            }

            if (!TryParsePolygon(request.PolygonGeoJson, out var polygon, out var parseError))
            {
                return Result<bool>.Failure(parseError!);
            }

            if (!targetPlot.IsBoundaryWithinTolerance(polygon!, TolerancePercent, out var drawnAreaHa, out var differencePercent))
            {
                return Result<bool>.Failure(
                    $"Polygon area validation failed. Drawn area ({Math.Round(drawnAreaHa, 2)} ha) differs by {Math.Round(differencePercent, 2)}% from registered plot area ({Math.Round(targetPlot.Area, 2)} ha). Maximum allowed difference is {TolerancePercent}%.");
            }

            targetPlot.AssignBoundary(polygon!);

            polygonTask.Status = "Completed";
            polygonTask.CompletedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Notes))
            {
                polygonTask.Notes = request.Notes;
            }

            await _context.SaveChangesAsync(cancellationToken);

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

