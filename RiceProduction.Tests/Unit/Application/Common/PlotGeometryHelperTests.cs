using FluentAssertions;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Helpers;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.Common;

public class PlotGeometryHelperTests
{
    private readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), PlotGeometryHelper.Wgs84Srid);

    private Polygon CreateSquarePolygon(double sideInDegrees) =>
        _geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, sideInDegrees),
            new Coordinate(sideInDegrees, sideInDegrees),
            new Coordinate(sideInDegrees, 0),
            new Coordinate(0, 0)
        });

    #region CalculateAreaInHectares

    [Fact]
    public void CalculateAreaInHectares_KnownOneDegreeSquare_Unrounded_ReturnsExpectedArea()
    {
        // Arrange
        var polygon = CreateSquarePolygon(1.0);
        var expected = (decimal)(1.0 * PlotGeometryHelper.MetersPerDegree * PlotGeometryHelper.MetersPerDegree / 10000.0);

        // Act
        var result = PlotGeometryHelper.CalculateAreaInHectares(polygon);

        // Assert
        result.Should().Be(expected);
        result.Should().BeApproximately(1239212.013601m, 0.0001m);
    }

    [Fact]
    public void CalculateAreaInHectares_KnownSmallSquarePolygon_UnroundedVsTwoDecimalPlaces_ReturnsExpectedAreas()
    {
        // Arrange
        var polygon = CreateSquarePolygon(0.001);
        var expectedUnrounded = (decimal)(polygon.Area * PlotGeometryHelper.MetersPerDegree * PlotGeometryHelper.MetersPerDegree / 10000.0);

        // Act
        var unrounded = PlotGeometryHelper.CalculateAreaInHectares(polygon);
        var rounded = PlotGeometryHelper.CalculateAreaInHectares(polygon, decimalPlaces: 2);

        // Assert
        unrounded.Should().Be(expectedUnrounded);
        unrounded.Should().NotBe(rounded);
        rounded.Should().Be(1.24m);
        rounded.Should().Be(Math.Round(unrounded, 2));
    }

    #endregion

    #region CreateDefaultBoundary

    [Fact]
    public void CreateDefaultBoundary_ReturnsValidClosedPolygonWithFiveCoordinates()
    {
        // Act
        var polygon = PlotGeometryHelper.CreateDefaultBoundary(_geometryFactory);

        // Assert
        polygon.Should().NotBeNull();
        polygon.IsValid.Should().BeTrue();
        polygon.ExteriorRing.IsClosed.Should().BeTrue();
        polygon.Coordinates.Should().HaveCount(5);
        polygon.Coordinates[0].Should().Be(polygon.Coordinates[^1]);
        polygon.SRID.Should().Be(PlotGeometryHelper.Wgs84Srid);
    }

    #endregion

    #region SerializeGeometry

    [Fact]
    public void SerializeGeometry_NullGeometry_ReturnsNull()
    {
        // Act
        var result = PlotGeometryHelper.SerializeGeometry(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SerializeGeometry_ValidPolygon_ReturnsGeoJsonContainingPolygon()
    {
        // Arrange
        var polygon = PlotGeometryHelper.CreateDefaultBoundary(_geometryFactory);

        // Act
        var result = PlotGeometryHelper.SerializeGeometry(polygon);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("\"Polygon\"");
        result.Should().Contain("coordinates");
    }

    #endregion
}
