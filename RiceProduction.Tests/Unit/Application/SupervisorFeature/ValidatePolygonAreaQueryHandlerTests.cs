using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.SupervisorFeature.Queries.ValidatePolygonArea;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using System.Linq.Expressions;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.SupervisorFeature;

/// <summary>
/// Tests for ValidatePolygonAreaQueryHandler - validates polygon area against registered plot area
/// </summary>
public class ValidatePolygonAreaQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Plot>> _mockPlotRepo;
    private readonly Mock<ILogger<ValidatePolygonAreaQueryHandler>> _mockLogger;
    private readonly ValidatePolygonAreaQueryHandler _handler;

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

    public ValidatePolygonAreaQueryHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPlotRepo = new Mock<IGenericRepository<Plot>>();
        _mockLogger = new Mock<ILogger<ValidatePolygonAreaQueryHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<Plot>()).Returns(_mockPlotRepo.Object);

        _handler = new ValidatePolygonAreaQueryHandler(_mockUnitOfWork.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_PlotNotFound_ReturnsFailure()
    {
        // Arrange
        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync((Plot?)null);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = Guid.NewGuid(),
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 10m
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Plot not found");
    }

    [Fact]
    public async Task Handle_InvalidGeoJson_ReturnsFailure()
    {
        // Arrange
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000m, status: PlotStatus.Active);
        plot.Area = 1.0m;

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(plot);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = "{ invalid json }",
            TolerancePercent = 10m
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid GeoJSON format"));
    }

    [Fact]
    public async Task Handle_NonPolygonGeometry_ReturnsFailure()
    {
        // Arrange
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000m, status: PlotStatus.Active);
        plot.Area = 1.0m;

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(plot);

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
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid polygon geometry. Must be a Polygon type.");
    }

    [Fact]
    public async Task Handle_ValidPolygonWithinTolerance_ReturnsSuccess()
    {
        // Arrange
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000m, status: PlotStatus.Active);
        plot.Area = 1.2m; // 1.2 hectares registered

        // Mock the ONE-parameter FindAsync that the handler actually calls
        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(plot);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson, // ~1.2 hectares (approximately)
            TolerancePercent = 10m // 10% tolerance
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

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
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000m, status: PlotStatus.Active);
        plot.Area = 1.0m; // 1 hectare registered

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(plot);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = LargerPolygonGeoJson, // ~2 hectares (100% difference)
            TolerancePercent = 10m // Only 10% tolerance allowed
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

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
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000m, status: PlotStatus.Active);
        plot.Area = 1.5m;

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(plot);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 25m // Custom 25% tolerance
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.TolerancePercent.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_CalculatesCorrectDifferencePercent()
    {
        // Arrange
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000m, status: PlotStatus.Active);
        plot.Area = 10.0m; // 10 hectares registered

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(plot);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 100m // Large tolerance for test
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

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
        var plotId = Guid.NewGuid();
        var plot = MockDataBuilder.CreatePlot(id: plotId, farmerId: Guid.NewGuid(), area: 1000m, status: PlotStatus.Active);
        plot.Area = 1.234567m; // More than 2 decimals

        _mockPlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plot, bool>>>()))
            .ReturnsAsync(plot);

        var query = new ValidatePolygonAreaQuery
        {
            PlotId = plotId,
            PolygonGeoJson = ValidPolygonGeoJson,
            TolerancePercent = 50m
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.PlotAreaHa.Should().Be(1.23m); // Rounded to 2 decimals
        // Drawn area and difference should also be rounded to 2 decimals
        result.Data.DrawnAreaHa.ToString().Split('.').LastOrDefault()?.Length.Should().BeLessThanOrEqualTo(2);
        result.Data.DifferencePercent.ToString().Split('.').LastOrDefault()?.Length.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var handler = new ValidatePolygonAreaQueryHandler(_mockUnitOfWork.Object, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
