using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace RiceProduction.Application.Common.Helpers;

public static class PlotGeometryHelper
{
    public const double MetersPerDegree = 111319.9;
    public const int Wgs84Srid = 4326;

    /// <summary>
    /// Approximates polygon area in hectares from WGS84 square degrees (1 degree ≈ 111,319.9 m at equator, 1 ha = 10,000 m²).
    /// </summary>
    public static decimal CalculateAreaInHectares(Polygon polygon, int? decimalPlaces = null)
    {
        var areaInSquareMeters = polygon.Area * MetersPerDegree * MetersPerDegree;
        var hectares = (decimal)(areaInSquareMeters / 10000.0);

        return decimalPlaces.HasValue
            ? Math.Round(hectares, decimalPlaces.Value)
            : hectares;
    }

    /// <summary>
    /// Creates a placeholder polygon boundary for plots awaiting field polygon assignment.
    /// </summary>
    public static Polygon CreateDefaultBoundary(GeometryFactory geometryFactory) =>
        geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 0.001),
            new Coordinate(0.001, 0.001),
            new Coordinate(0.001, 0),
            new Coordinate(0, 0)
        });

    public static string? SerializeGeometry(Geometry? geometry)
    {
        if (geometry == null)
        {
            return null;
        }

        try
        {
            return new GeoJsonWriter().Write(geometry);
        }
        catch
        {
            return null;
        }
    }
}
