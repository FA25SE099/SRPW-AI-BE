using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.SupervisorFeature.Queries.ValidatePolygonArea;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.SupervisorFeature;

/// <summary>
/// Tests for ValidatePolygonAreaQueryHandler - validates polygon area against registered plot area
/// </summary>
public class ValidatePolygonAreaQueryHandlerTests
{
    private readonly Mock<ILogger<ValidatePolygonAreaQueryHandler>> _mockLogger = new();

    // Sample GeoJSON for a small polygon (~1 hectare)
    private const string ValidPolygonGeoJson = @"{
        ""type"": ""Polygon"",
        ""coordinates"": [[
            [106.0, 10.0],
            [106.001, 10.0],
            [106.001, 10.001],
            [106.0, 10.001],
            [106.0, 10.0]
        ]]
    }";

    // Sample GeoJSON for a larger polygon (~2 hectares)
    private const string LargerPolygonGeoJson = @"{
        ""type"": ""Polygon"",
        ""coordinates"": [[
            [106.0, 10.0],
            [106.002, 10.0],
            [106.002, 10.001],
            [106.0, 10.001],
            [106.0, 10.0]
        ]]
    }";

    [Fact]
    public async Task Handle_PlotNotFound_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = Guid.NewGuid(),
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 10m
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Plot not found");
    }

    [Fact]
    public async Task Handle_InvalidGeoJson_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1.0m, status: PlotStatus.Active);
        context.Plots.Add(plot);
        await context.SaveChangesAsync();

        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = "{ invalid json }",
            TolerancePercent = 10m
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid GeoJSON format"));
    }

    [Fact]
    public async Task Handle_NonPolygonGeometry_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1.0m, status: PlotStatus.Active);
        context.Plots.Add(plot);
        await context.SaveChangesAsync();

        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        // GeoJSON for a Point, not a Polygon
        var pointGeoJson = @"{
            ""type"": ""Point"",
            ""coordinates"": [106.0, 10.0]
        }";

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = pointGeoJson,
            TolerancePercent = 10m
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid polygon geometry. Must be a Polygon type.");
    }

    [Fact]
    public async Task Handle_ValidPolygonWithinTolerance_ReturnsSuccess()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1.2m, status: PlotStatus.Active);
        context.Plots.Add(plot);
        await context.SaveChangesAsync();

        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson, // ~1.2 hectares (approximately)
            TolerancePercent = 10m // 10% tolerance
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsValid.Should().BeTrue();
        result.Data.PlotAreaHa.Should().Be(1.2m);
        result.Data.DrawnAreaHa.Should().BeGreaterThan(0);
        result.Data.TolerancePercent.Should().Be(10m);
        result.Data.Message.Should().Contain("within acceptable tolerance");
    }

    [Fact]
    public async Task Handle_PolygonExceedsTolerance_ReturnsFailure()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1.0m, status: PlotStatus.Active);
        context.Plots.Add(plot);
        await context.SaveChangesAsync();

        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = LargerPolygonGeoJson, // ~2 hectares (100% difference)
            TolerancePercent = 10m // Only 10% tolerance allowed
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue(); // Request succeeds but validation fails
        result.Data.Should().NotBeNull();
        result.Data.IsValid.Should().BeFalse();
        result.Data.DifferencePercent.Should().BeGreaterThan(10m);
        result.Data.Message.Should().Contain("differs by");
        result.Data.Message.Should().Contain("Maximum allowed is 10%");
    }

    [Fact]
    public async Task Handle_CustomTolerancePercent_UsesProvidedTolerance()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1.5m, status: PlotStatus.Active);
        context.Plots.Add(plot);
        await context.SaveChangesAsync();

        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 25m // Custom 25% tolerance
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.TolerancePercent.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_CalculatesCorrectDifferencePercent()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 10.0m, status: PlotStatus.Active);
        context.Plots.Add(plot);
        await context.SaveChangesAsync();

        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 100m // Large tolerance for test
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.PlotAreaHa.Should().Be(10.0m);
        result.Data.DrawnAreaHa.Should().BeGreaterThan(0);
        // DifferencePercent should be calculated as: |DrawnArea - PlotArea| / PlotArea * 100
        var expectedDifferencePercent = Math.Abs(result.Data.DrawnAreaHa - result.Data.PlotAreaHa) / result.Data.PlotAreaHa * 100;
        result.Data.DifferencePercent.Should().BeApproximately(expectedDifferencePercent, 0.1m);
    }

    [Fact]
    public async Task Handle_RoundsAreasAndPercentagesToTwoDecimals()
    {
        // Arrange
        await using var context = TestApplicationDbContext.Create();
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1.234567m, status: PlotStatus.Active);
        context.Plots.Add(plot);
        await context.SaveChangesAsync();

        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 50m
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.PlotAreaHa.Should().Be(1.23m); // Rounded to 2 decimals
        // Drawn area and difference should also be rounded to 2 decimals
        result.Data.DrawnAreaHa.ToString().Split('.').LastOrDefault()?.Length.Should().BeLessThanOrEqualTo(2);
        result.Data.DifferencePercent.ToString().Split('.').LastOrDefault()?.Length.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        await using var context = TestApplicationDbContext.Create();
        var handler = new ValidatePolygonAreaQueryHandler(context, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
