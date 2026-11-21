using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Queries.ValidatePolygonArea;

public class ValidatePolygonAreaQueryHandler : IRequestHandler<ValidatePolygonAreaQuery, Result<PolygonValidationResponse>>
{
    private const decimal DefaultTolerancePercent = 10;

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
            // When caller passes the default value, fall back to the configured system setting
            var tolerancePercent = request.TolerancePercent == DefaultTolerancePercent
                ? await GetToleranceFromSystemSettings()
                : request.TolerancePercent;

            var plot = await _unitOfWork.Repository<Plot>()
                .FindAsync(p => p.Id == request.PlotId);

            if (plot == null)
            {
                return Result<PolygonValidationResponse>.Failure("Plot not found");
            }

            if (!TryParsePolygon(request.PolygonGeoJson, request.PlotId, out var polygon, out var parseError))
            {
                return Result<PolygonValidationResponse>.Failure(parseError!);
            }

            var drawnAreaHa = PlotGeometryHelper.CalculateAreaInHectares(polygon!);
            var plotAreaHa = plot.Area;
            var difference = Math.Abs(drawnAreaHa - plotAreaHa);
            var differencePercent = plotAreaHa > 0 ? (difference / plotAreaHa) * 100 : 0;
            var roundedDifferencePercent = Math.Round(differencePercent, 2);
            var isValid = differencePercent <= tolerancePercent;

            var response = new PolygonValidationResponse
            {
                IsValid = isValid,
                DrawnAreaHa = Math.Round(drawnAreaHa, 2),
                PlotAreaHa = Math.Round(plotAreaHa, 2),
                DifferencePercent = roundedDifferencePercent,
                TolerancePercent = tolerancePercent,
                Message = isValid
                    ? "Polygon area is within acceptable tolerance"
                    : $"Polygon area differs by {roundedDifferencePercent}% from registered plot area. Maximum allowed is {tolerancePercent}%"
            };

            return Result<PolygonValidationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating polygon area for plot {PlotId}", request.PlotId);
            return Result<PolygonValidationResponse>.Failure($"Error validating polygon: {ex.Message}");
        }
    }

    private bool TryParsePolygon(string polygonGeoJson, Guid plotId, out Polygon? polygon, out string? error)
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
            _logger.LogError(ex, "Error parsing polygon GeoJSON for plot {PlotId}", plotId);
            error = $"Invalid GeoJSON format: {ex.Message}";
            return false;
        }
    }

    private async Task<decimal> GetToleranceFromSystemSettings()
    {
        try
        {
            var setting = await _unitOfWork.Repository<SystemSetting>()
                .FindAsync(s => s.SettingKey == "PolygonAreaTolerancePercent");

            if (setting != null && decimal.TryParse(setting.SettingValue, out var value))
            {
                return value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve PolygonAreaTolerancePercent from system settings, using default value");
        }

        return DefaultTolerancePercent;
    }
}

