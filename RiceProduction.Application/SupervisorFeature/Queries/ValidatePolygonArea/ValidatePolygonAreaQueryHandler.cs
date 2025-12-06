using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Queries.ValidatePolygonArea;

public class ValidatePolygonAreaQueryHandler : IRequestHandler<ValidatePolygonAreaQuery, Result<PolygonValidationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ValidatePolygonAreaQueryHandler> _logger;

    public ValidatePolygonAreaQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<ValidatePolygonAreaQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PolygonValidationResponse>> Handle(ValidatePolygonAreaQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get the plot
            var plot = await _unitOfWork.Repository<Plot>()
                .FindAsync(p => p.Id == request.PlotId);

            if (plot == null)
            {
                return Result<PolygonValidationResponse>.Failure("Plot not found");
            }

            // Parse GeoJSON to Polygon geometry
            Polygon polygon;
            try
            {
                var geoJsonReader = new GeoJsonReader();
                var geometry = geoJsonReader.Read<Geometry>(request.PolygonGeoJson);

                polygon = geometry as Polygon;
                if (polygon == null)
                {
                    return Result<PolygonValidationResponse>.Failure("Invalid polygon geometry. Must be a Polygon type.");
                }

                // Set SRID to 4326 (WGS84)
                polygon.SRID = 4326;

                // Validate polygon
                if (!polygon.IsValid)
                {
                    return Result<PolygonValidationResponse>.Failure("Invalid polygon geometry. Polygon is not geometrically valid.");
                }

                if (polygon.IsEmpty)
                {
                    return Result<PolygonValidationResponse>.Failure("Invalid polygon geometry. Polygon is empty.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing polygon GeoJSON for plot {PlotId}", request.PlotId);
                return Result<PolygonValidationResponse>.Failure($"Invalid GeoJSON format: {ex.Message}");
            }

            // Calculate area from polygon (in hectares)
            // The polygon.Area gives us area in square degrees for WGS84
            // We need to convert to square meters, then to hectares
            var drawnAreaHa = CalculateAreaInHectares(polygon);

            // Get plot's registered area (already in hectares)
            var plotAreaHa = plot.Area;

            // Calculate difference percentage
            var difference = Math.Abs(drawnAreaHa - plotAreaHa);
            var differencePercent = plotAreaHa > 0 ? (difference / plotAreaHa) * 100 : 0;

            // Check if within tolerance
            var isValid = differencePercent <= request.TolerancePercent;

            var response = new PolygonValidationResponse
            {
                IsValid = isValid,
                DrawnAreaHa = Math.Round(drawnAreaHa, 2),
                PlotAreaHa = Math.Round(plotAreaHa, 2),
                DifferencePercent = Math.Round(differencePercent, 2),
                TolerancePercent = request.TolerancePercent,
                Message = isValid
                    ? "Polygon area is within acceptable tolerance"
                    : $"Polygon area differs by {Math.Round(differencePercent, 2)}% from registered plot area. Maximum allowed is {request.TolerancePercent}%"
            };

            return Result<PolygonValidationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating polygon area for plot {PlotId}", request.PlotId);
            return Result<PolygonValidationResponse>.Failure($"Error validating polygon: {ex.Message}");
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

