using FluentAssertions;
using NetTopologySuite.Geometries;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using Xunit;
using DomainTaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Tests.Unit.Domain;

public class DomainEntitiesTests
{
    private readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), Plot.Wgs84Srid);

    private Polygon CreateSquarePolygon(double sideInDegrees, double originX = 0, double originY = 0) =>
        _geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(originX, originY),
            new Coordinate(originX, originY + sideInDegrees),
            new Coordinate(originX + sideInDegrees, originY + sideInDegrees),
            new Coordinate(originX + sideInDegrees, originY),
            new Coordinate(originX, originY)
        });

    private Polygon CreateSelfIntersectingPolygon() =>
        _geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 1),
            new Coordinate(1, 0),
            new Coordinate(0, 1),
            new Coordinate(0, 0)
        });

    #region Season

    [Theory]
    [InlineData("01/01", 1, 1)]
    [InlineData("12/31", 12, 31)]
    [InlineData("5/9", 5, 9)]
    [InlineData("06/15", 6, 15)]
    public void Season_TryParseMonthDay_ValidInput_ReturnsTrueAndParsesValues(
        string input,
        int expectedMonth,
        int expectedDay)
    {
        // Act
        var result = Season.TryParseMonthDay(input, out var month, out var day);

        // Assert
        result.Should().BeTrue();
        month.Should().Be(expectedMonth);
        day.Should().Be(expectedDay);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12")]
    [InlineData("12/31/2025")]
    [InlineData("00/15")]
    [InlineData("13/01")]
    [InlineData("05/00")]
    [InlineData("05/32")]
    [InlineData("ab/cd")]
    [InlineData("05/ab")]
    [InlineData("ab/05")]
    [InlineData("05-15")]
    public void Season_TryParseMonthDay_InvalidInput_ReturnsFalse(string? input)
    {
        // Act
        var result = Season.TryParseMonthDay(input, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(4, 1, true)]
    [InlineData(6, 15, true)]
    [InlineData(8, 31, true)]
    [InlineData(3, 31, false)]
    [InlineData(9, 1, false)]
    public void Season_IsDateInRange_SameYearRange_ReturnsExpectedResult(
        int month,
        int day,
        bool expected)
    {
        // Act
        var result = Season.IsDateInRange(month, day, "04/01", "08/31");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(11, 1, true)]
    [InlineData(12, 25, true)]
    [InlineData(1, 15, true)]
    [InlineData(2, 28, true)]
    [InlineData(3, 1, false)]
    [InlineData(6, 15, false)]
    [InlineData(10, 31, false)]
    public void Season_IsDateInRange_CrossYearRange_ReturnsExpectedResult(
        int month,
        int day,
        bool expected)
    {
        // Act
        var result = Season.IsDateInRange(month, day, "11/01", "02/28");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid", "08/31")]
    [InlineData("04/01", "invalid")]
    [InlineData("", "")]
    public void Season_IsDateInRange_InvalidDateStrings_ReturnsFalse(
        string startDate,
        string endDate)
    {
        // Act
        var result = Season.IsDateInRange(6, 15, startDate, endDate);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(5, 1, true)]
    [InlineData(7, 15, true)]
    [InlineData(9, 30, true)]
    [InlineData(4, 30, false)]
    [InlineData(10, 1, false)]
    public void Season_ContainsDate_SameYearSeason_ReturnsExpectedResultForBothOverloads(
        int month,
        int day,
        bool expected)
    {
        // Arrange
        var season = new Season { StartDate = "05/01", EndDate = "09/30" };
        var date = new DateTime(2025, month, day);

        // Act & Assert
        season.ContainsDate(month, day).Should().Be(expected);
        season.ContainsDate(date).Should().Be(expected);
    }

    [Theory]
    [InlineData(11, 15, true)]
    [InlineData(12, 20, true)]
    [InlineData(1, 10, true)]
    [InlineData(3, 15, true)]
    [InlineData(3, 16, false)]
    [InlineData(6, 1, false)]
    [InlineData(11, 14, false)]
    public void Season_ContainsDate_CrossYearSeason_ReturnsExpectedResultForBothOverloads(
        int month,
        int day,
        bool expected)
    {
        // Arrange
        var season = new Season { StartDate = "11/15", EndDate = "03/15" };
        var date = new DateTime(2025, month, day);

        // Act & Assert
        season.ContainsDate(month, day).Should().Be(expected);
        season.ContainsDate(date).Should().Be(expected);
    }

    [Fact]
    public void Season_TryGetDateRange_SameYearSeason_ReturnsStartAndEndInSameYear()
    {
        // Arrange
        var season = new Season { StartDate = "05/01", EndDate = "09/30" };

        // Act
        var result = season.TryGetDateRange(2025, out var startDate, out var endDate);

        // Assert
        result.Should().BeTrue();
        startDate.Should().Be(new DateTime(2025, 5, 1));
        endDate.Should().Be(new DateTime(2025, 9, 30));
    }

    [Fact]
    public void Season_TryGetDateRange_CrossYearSeason_ReturnsEndInFollowingYear()
    {
        // Arrange
        var season = new Season { StartDate = "11/15", EndDate = "03/15" };

        // Act
        var result = season.TryGetDateRange(2025, out var startDate, out var endDate);

        // Assert
        result.Should().BeTrue();
        startDate.Should().Be(new DateTime(2025, 11, 15));
        endDate.Should().Be(new DateTime(2026, 3, 15));
    }

    [Theory]
    [InlineData("invalid", "09/30")]
    [InlineData("05/01", "invalid")]
    [InlineData("02/30", "05/01")]
    [InlineData("02/29", "05/01")]
    public void Season_TryGetDateRange_InvalidFormatOrCalendarDate_ReturnsFalse(
        string startDateStr,
        string endDateStr)
    {
        // Arrange
        var season = new Season { StartDate = startDateStr, EndDate = endDateStr };

        // Act
        var result = season.TryGetDateRange(2025, out var startDate, out var endDate);

        // Assert
        result.Should().BeFalse();
        startDate.Should().Be(default);
        endDate.Should().Be(default);
    }

    [Fact]
    public void Season_ResolveYearForDate_CrossYearSeason_ResolvesPreviousYearForJanuaryAndSameYearForDecember()
    {
        // Arrange
        var season = new Season { StartDate = "11/01", EndDate = "03/31" };

        // Act & Assert
        season.ResolveYearForDate(new DateTime(2025, 11, 1)).Should().Be(2025);
        season.ResolveYearForDate(new DateTime(2025, 12, 15)).Should().Be(2025);
        season.ResolveYearForDate(new DateTime(2026, 1, 15)).Should().Be(2025);
        season.ResolveYearForDate(new DateTime(2026, 3, 31)).Should().Be(2025);
    }

    [Fact]
    public void Season_ResolveYearForDate_EarlyYearSeason_DoesNotDecrementYear()
    {
        // Arrange
        var season = new Season { StartDate = "05/01", EndDate = "09/30" };

        // Act & Assert
        season.ResolveYearForDate(new DateTime(2025, 2, 10)).Should().Be(2025);
        season.ResolveYearForDate(new DateTime(2025, 7, 10)).Should().Be(2025);
    }

    [Fact]
    public void Season_ResolveYearForDate_InvalidStartDate_ReturnsReferenceYear()
    {
        // Arrange
        var season = new Season { StartDate = "invalid", EndDate = "03/31" };

        // Act
        var resolvedYear = season.ResolveYearForDate(new DateTime(2026, 1, 15));

        // Assert
        resolvedYear.Should().Be(2026);
    }

    #endregion

    #region Plot

    [Fact]
    public void Plot_HasValidBoundary_NullBoundary_ReturnsFalse()
    {
        // Arrange
        var plot = new Plot { Boundary = null };

        // Act & Assert
        plot.HasValidBoundary().Should().BeFalse();
    }

    [Fact]
    public void Plot_HasValidBoundary_ValidNonEmptyPolygon_ReturnsTrue()
    {
        // Arrange
        var plot = new Plot { Boundary = CreateSquarePolygon(0.001) };

        // Act & Assert
        plot.HasValidBoundary().Should().BeTrue();
    }

    [Fact]
    public void Plot_HasValidBoundary_EmptyPolygon_ReturnsFalse()
    {
        // Arrange
        var plot = new Plot { Boundary = _geometryFactory.CreatePolygon() };

        // Act & Assert
        plot.HasValidBoundary().Should().BeFalse();
    }

    [Fact]
    public void Plot_HasValidBoundary_InvalidSelfIntersectingPolygon_ReturnsFalse()
    {
        // Arrange
        var plot = new Plot { Boundary = CreateSelfIntersectingPolygon() };

        // Act & Assert
        plot.HasValidBoundary().Should().BeFalse();
    }

    [Fact]
    public void Plot_CalculatePolygonAreaInHectares_ReturnsUnroundedAndRoundedAreas()
    {
        // Arrange
        var polygon = CreateSquarePolygon(0.001);
        var expectedUnrounded = (decimal)(polygon.Area * Plot.MetersPerDegree * Plot.MetersPerDegree / 10000.0);

        // Act
        var unrounded = Plot.CalculatePolygonAreaInHectares(polygon);
        var roundedToTwo = Plot.CalculatePolygonAreaInHectares(polygon, decimalPlaces: 2);
        var roundedToFour = Plot.CalculatePolygonAreaInHectares(polygon, decimalPlaces: 4);

        // Assert
        unrounded.Should().Be(expectedUnrounded);
        roundedToTwo.Should().Be(1.24m);
        roundedToTwo.Should().Be(Math.Round(unrounded, 2));
        roundedToFour.Should().Be(1.2392m);
        roundedToFour.Should().Be(Math.Round(unrounded, 4));
    }

    [Fact]
    public void Plot_IsBoundaryWithinTolerance_WithinTolerance_ReturnsTrueWithExpectedOutputs()
    {
        // Arrange
        var polygon = CreateSquarePolygon(0.001);
        var expectedDrawnArea = Plot.CalculatePolygonAreaInHectares(polygon);
        var plot = new Plot { Area = 1.25m };
        var expectedDiffPercent = Math.Abs(expectedDrawnArea - plot.Area) / plot.Area * 100m;

        // Act
        var result = plot.IsBoundaryWithinTolerance(
            polygon,
            tolerancePercent: 5m,
            out var drawnAreaHa,
            out var differencePercent);

        // Assert
        result.Should().BeTrue();
        drawnAreaHa.Should().Be(expectedDrawnArea);
        differencePercent.Should().Be(expectedDiffPercent);
        differencePercent.Should().BeLessThanOrEqualTo(5m);
    }

    [Fact]
    public void Plot_IsBoundaryWithinTolerance_OutsideTolerance_ReturnsFalseWithExpectedOutputs()
    {
        // Arrange
        var polygon = CreateSquarePolygon(0.001);
        var expectedDrawnArea = Plot.CalculatePolygonAreaInHectares(polygon);
        var plot = new Plot { Area = 2.0m };
        var expectedDiffPercent = Math.Abs(expectedDrawnArea - plot.Area) / plot.Area * 100m;

        // Act
        var result = plot.IsBoundaryWithinTolerance(
            polygon,
            tolerancePercent: 10m,
            out var drawnAreaHa,
            out var differencePercent);

        // Assert
        result.Should().BeFalse();
        drawnAreaHa.Should().Be(expectedDrawnArea);
        differencePercent.Should().Be(expectedDiffPercent);
        differencePercent.Should().BeGreaterThan(10m);
    }

    [Fact]
    public void Plot_IsBoundaryWithinTolerance_ZeroRegisteredArea_ReturnsZeroDifferenceAndTrue()
    {
        // Arrange
        var polygon = CreateSquarePolygon(0.001);
        var expectedDrawnArea = Plot.CalculatePolygonAreaInHectares(polygon);
        var plot = new Plot { Area = 0m };

        // Act
        var result = plot.IsBoundaryWithinTolerance(
            polygon,
            tolerancePercent: 0m,
            out var drawnAreaHa,
            out var differencePercent);

        // Assert
        result.Should().BeTrue();
        drawnAreaHa.Should().Be(expectedDrawnArea);
        differencePercent.Should().Be(0m);
    }

    [Fact]
    public void Plot_AssignBoundary_ValidPolygon_SetsBoundaryCentroidWithWgs84SridAndActiveStatus()
    {
        // Arrange
        var polygon = CreateSquarePolygon(sideInDegrees: 0.002, originX: 105.0, originY: 10.0);
        var plot = new Plot
        {
            Status = PlotStatus.PendingPolygon,
            Boundary = null,
            Coordinate = null
        };

        // Act
        plot.AssignBoundary(polygon);

        // Assert
        plot.Boundary.Should().BeSameAs(polygon);
        plot.Coordinate.Should().NotBeNull();
        plot.Coordinate!.SRID.Should().Be(Plot.Wgs84Srid);
        plot.Coordinate.X.Should().BeApproximately(105.001, 1e-9);
        plot.Coordinate.Y.Should().BeApproximately(10.001, 1e-9);
        plot.Status.Should().Be(PlotStatus.Active);
    }

    [Fact]
    public void Plot_AssignBoundary_EmptyPolygon_SetsBoundaryAndActiveStatusWithoutSettingCoordinate()
    {
        // Arrange
        var emptyPolygon = _geometryFactory.CreatePolygon();
        var plot = new Plot
        {
            Status = PlotStatus.PendingPolygon,
            Coordinate = null
        };

        // Act
        plot.AssignBoundary(emptyPolygon);

        // Assert
        plot.Boundary.Should().BeSameAs(emptyPolygon);
        plot.Coordinate.Should().BeNull();
        plot.Status.Should().Be(PlotStatus.Active);
    }

    [Fact]
    public void Plot_MarkPendingPolygon_SetsDefaultBoundaryAndPendingPolygonStatus()
    {
        // Arrange
        var defaultBoundary = CreateSquarePolygon(0.0001);
        var plot = new Plot { Status = PlotStatus.Active };

        // Act
        plot.MarkPendingPolygon(defaultBoundary);

        // Assert
        plot.Boundary.Should().BeSameAs(defaultBoundary);
        plot.Status.Should().Be(PlotStatus.PendingPolygon);
    }

    #endregion

    #region Material

    public static TheoryData<decimal?, decimal> EffectiveAmountPerMaterialData => new()
    {
        { 50m, 50m },
        { 0.5m, 0.5m },
        { null, 1m },
        { 0m, 1m },
        { -10m, 1m }
    };

    [Theory]
    [MemberData(nameof(EffectiveAmountPerMaterialData))]
    public void Material_EffectiveAmountPerMaterial_ReturnsPositiveAmountOrDefaultsToOne(
        decimal? amountPerMaterial,
        decimal expectedEffectiveAmount)
    {
        // Arrange
        var material = new Material { AmmountPerMaterial = amountPerMaterial };

        // Act & Assert
        material.EffectiveAmountPerMaterial.Should().Be(expectedEffectiveAmount);
    }

    public static TheoryData<decimal, decimal?, decimal> PartitionPackagesData => new()
    {
        { 25m, 10m, 2.5m },
        { 75m, 50m, 1.5m },
        { 20m, 10m, 2m },
        { 0m, 10m, 0m },
        { 2.5m, null, 2.5m },
        { 2.5m, 0m, 2.5m }
    };

    [Theory]
    [MemberData(nameof(PartitionPackagesData))]
    public void Material_CalculatePackagesNeeded_PartitionEnabled_ReturnsExactFractionalPackages(
        decimal totalQuantityNeeded,
        decimal? amountPerMaterial,
        decimal expectedPackages)
    {
        // Arrange
        var material = new Material
        {
            AmmountPerMaterial = amountPerMaterial,
            IsPartition = true
        };

        // Act
        var packages = material.CalculatePackagesNeeded(totalQuantityNeeded);

        // Assert
        packages.Should().Be(expectedPackages);
    }

    public static TheoryData<decimal, decimal?, decimal> NonPartitionPackagesData => new()
    {
        { 25m, 10m, 3m },
        { 20.1m, 10m, 3m },
        { 20m, 10m, 2m },
        { 1m, 50m, 1m },
        { 0m, 10m, 0m },
        { 2.1m, null, 3m },
        { 2.1m, -5m, 3m }
    };

    [Theory]
    [MemberData(nameof(NonPartitionPackagesData))]
    public void Material_CalculatePackagesNeeded_PartitionDisabled_ReturnsCeilingPackages(
        decimal totalQuantityNeeded,
        decimal? amountPerMaterial,
        decimal expectedPackages)
    {
        // Arrange
        var material = new Material
        {
            AmmountPerMaterial = amountPerMaterial,
            IsPartition = false
        };

        // Act
        var packages = material.CalculatePackagesNeeded(totalQuantityNeeded);

        // Assert
        packages.Should().Be(expectedPackages);
    }

    [Fact]
    public void Material_CalculateTotalCost_NonPartition_MultipliesCeilingPackagesByPrice()
    {
        // Arrange
        var material = new Material
        {
            AmmountPerMaterial = 50m,
            IsPartition = false
        };

        // Act
        var totalCost = material.CalculateTotalCost(totalQuantityNeeded: 75m, pricePerMaterial: 100_000m);

        // Assert
        totalCost.Should().Be(200_000m);
    }

    [Fact]
    public void Material_CalculateTotalCost_Partition_MultipliesFractionalPackagesByPrice()
    {
        // Arrange
        var material = new Material
        {
            AmmountPerMaterial = 50m,
            IsPartition = true
        };

        // Act
        var totalCost = material.CalculateTotalCost(totalQuantityNeeded: 75m, pricePerMaterial: 100_000m);

        // Assert
        totalCost.Should().Be(150_000m);
    }

    [Fact]
    public void Material_CalculateTotalCost_InvalidAmountPerMaterial_UsesDefaultEffectiveAmountOfOne()
    {
        // Arrange
        var material = new Material
        {
            AmmountPerMaterial = null,
            IsPartition = false
        };

        // Act
        var totalCost = material.CalculateTotalCost(totalQuantityNeeded: 12.2m, pricePerMaterial: 10_000m);

        // Assert
        totalCost.Should().Be(130_000m);
    }

    #endregion

    #region CultivationVersion

    [Fact]
    public void CultivationVersion_Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var activatedAt = new DateTime(2025, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var version = new CultivationVersion
        {
            IsActive = true,
            ActivatedAt = activatedAt
        };

        // Act
        version.Deactivate();

        // Assert
        version.IsActive.Should().BeFalse();
        version.ActivatedAt.Should().Be(activatedAt);
    }

    [Fact]
    public void CultivationVersion_Activate_WithExplicitTimestamp_SetsActiveAndExactTimestamp()
    {
        // Arrange
        var explicitTime = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var version = new CultivationVersion
        {
            IsActive = false,
            ActivatedAt = null
        };

        // Act
        version.Activate(explicitTime);

        // Assert
        version.IsActive.Should().BeTrue();
        version.ActivatedAt.Should().Be(explicitTime);
    }

    [Fact]
    public void CultivationVersion_Activate_WithDefaultTimestamp_SetsActiveAndCurrentUtcTimestamp()
    {
        // Arrange
        var version = new CultivationVersion
        {
            IsActive = false,
            ActivatedAt = null
        };
        var before = DateTime.UtcNow;

        // Act
        version.Activate();
        var after = DateTime.UtcNow;

        // Assert
        version.IsActive.Should().BeTrue();
        version.ActivatedAt.Should().NotBeNull();
        version.ActivatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CultivationVersion_CreateNext_ActivateImmediatelyWithExplicitTimestamp_IncrementsOrderAndActivates()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();
        var explicitTime = new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var version = CultivationVersion.CreateNext(
            plotCultivationId,
            currentMaxOrder: 2,
            versionName: "v3 - Pest Adjustment",
            reason: "Adjusted spraying schedule",
            activateImmediately: true,
            activatedAt: explicitTime);

        // Assert
        version.PlotCultivationId.Should().Be(plotCultivationId);
        version.VersionOrder.Should().Be(3);
        version.VersionName.Should().Be("v3 - Pest Adjustment");
        version.Reason.Should().Be("Adjusted spraying schedule");
        version.IsActive.Should().BeTrue();
        version.ActivatedAt.Should().Be(explicitTime);
    }

    [Fact]
    public void CultivationVersion_CreateNext_ActivateImmediatelyWithDefaultTimestamp_SetsCurrentUtcTimestamp()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var version = CultivationVersion.CreateNext(
            plotCultivationId,
            currentMaxOrder: 0,
            versionName: "v1 - Initial");
        var after = DateTime.UtcNow;

        // Assert
        version.PlotCultivationId.Should().Be(plotCultivationId);
        version.VersionOrder.Should().Be(1);
        version.VersionName.Should().Be("v1 - Initial");
        version.Reason.Should().BeNull();
        version.IsActive.Should().BeTrue();
        version.ActivatedAt.Should().NotBeNull();
        version.ActivatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CultivationVersion_CreateNext_Inactive_IncrementsOrderAndLeavesActivatedAtNull()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();
        var ignoredTimestamp = new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var version = CultivationVersion.CreateNext(
            plotCultivationId,
            currentMaxOrder: 4,
            versionName: "v5 - Draft",
            reason: "Pending review",
            activateImmediately: false,
            activatedAt: ignoredTimestamp);

        // Assert
        version.PlotCultivationId.Should().Be(plotCultivationId);
        version.VersionOrder.Should().Be(5);
        version.VersionName.Should().Be("v5 - Draft");
        version.Reason.Should().Be("Pending review");
        version.IsActive.Should().BeFalse();
        version.ActivatedAt.Should().BeNull();
    }

    #endregion

    #region ProductionPlan

    [Theory]
    [InlineData(DomainTaskStatus.Draft, false)]
    [InlineData(DomainTaskStatus.PendingApproval, true)]
    [InlineData(DomainTaskStatus.Approved, false)]
    [InlineData(DomainTaskStatus.InProgress, false)]
    [InlineData(DomainTaskStatus.Completed, false)]
    [InlineData(DomainTaskStatus.Cancelled, false)]
    public void ProductionPlan_CanBeReviewed_ReturnsTrueOnlyWhenPendingApproval(
        DomainTaskStatus status,
        bool expected)
    {
        // Arrange
        var plan = new ProductionPlan { Status = status };

        // Act & Assert
        plan.CanBeReviewed.Should().Be(expected);
    }

    [Theory]
    [InlineData(DomainTaskStatus.Draft, false)]
    [InlineData(DomainTaskStatus.PendingApproval, false)]
    [InlineData(DomainTaskStatus.Approved, true)]
    [InlineData(DomainTaskStatus.InProgress, false)]
    [InlineData(DomainTaskStatus.Completed, false)]
    [InlineData(DomainTaskStatus.Cancelled, false)]
    public void ProductionPlan_IsApproved_ReturnsTrueOnlyWhenApproved(
        DomainTaskStatus status,
        bool expected)
    {
        // Arrange
        var plan = new ProductionPlan { Status = status };

        // Act & Assert
        plan.IsApproved.Should().Be(expected);
    }

    [Fact]
    public void ProductionPlan_Submit_WithExplicitTimestamp_SetsPendingApprovalStatusTimestampsAndUserId()
    {
        // Arrange
        var plan = new ProductionPlan { Status = DomainTaskStatus.Draft };
        var supervisorId = Guid.NewGuid();
        var submittedAt = new DateTime(2025, 8, 10, 14, 0, 0, DateTimeKind.Utc);

        // Act
        plan.Submit(supervisorId, submittedAt);

        // Assert
        plan.Status.Should().Be(DomainTaskStatus.PendingApproval);
        plan.CanBeReviewed.Should().BeTrue();
        plan.IsApproved.Should().BeFalse();
        plan.SubmittedBy.Should().Be(supervisorId);
        plan.SubmittedAt.Should().Be(submittedAt);
        plan.LastModified.Should().Be(new DateTimeOffset(submittedAt));
        plan.LastModifiedBy.Should().Be(supervisorId);
    }

    [Fact]
    public void ProductionPlan_Submit_WithDefaultTimestamp_SetsCurrentUtcTimestamp()
    {
        // Arrange
        var plan = new ProductionPlan { Status = DomainTaskStatus.Draft };
        var supervisorId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        plan.Submit(supervisorId);
        var after = DateTime.UtcNow;

        // Assert
        plan.Status.Should().Be(DomainTaskStatus.PendingApproval);
        plan.SubmittedBy.Should().Be(supervisorId);
        plan.SubmittedAt.Should().NotBeNull();
        plan.SubmittedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        plan.LastModified.Should().BeOnOrAfter(new DateTimeOffset(before)).And.BeOnOrBefore(new DateTimeOffset(after));
        plan.LastModifiedBy.Should().Be(supervisorId);
    }

    [Fact]
    public void ProductionPlan_Approve_WithExplicitTimestamp_SetsApprovedStatusTimestampsAndUserId()
    {
        // Arrange
        var plan = new ProductionPlan { Status = DomainTaskStatus.PendingApproval };
        var expertId = Guid.NewGuid();
        var approvedAt = new DateTime(2025, 8, 12, 9, 30, 0, DateTimeKind.Utc);

        // Act
        plan.Approve(expertId, approvedAt);

        // Assert
        plan.Status.Should().Be(DomainTaskStatus.Approved);
        plan.IsApproved.Should().BeTrue();
        plan.CanBeReviewed.Should().BeFalse();
        plan.ApprovedBy.Should().Be(expertId);
        plan.ApprovedAt.Should().Be(approvedAt);
        plan.LastModified.Should().Be(new DateTimeOffset(approvedAt));
        plan.LastModifiedBy.Should().Be(expertId);
    }

    [Fact]
    public void ProductionPlan_Approve_WithDefaultTimestamp_SetsCurrentUtcTimestamp()
    {
        // Arrange
        var plan = new ProductionPlan { Status = DomainTaskStatus.PendingApproval };
        var expertId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        plan.Approve(expertId);
        var after = DateTime.UtcNow;

        // Assert
        plan.Status.Should().Be(DomainTaskStatus.Approved);
        plan.ApprovedBy.Should().Be(expertId);
        plan.ApprovedAt.Should().NotBeNull();
        plan.ApprovedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        plan.LastModified.Should().BeOnOrAfter(new DateTimeOffset(before)).And.BeOnOrBefore(new DateTimeOffset(after));
        plan.LastModifiedBy.Should().Be(expertId);
    }

    [Fact]
    public void ProductionPlan_Reject_WithExplicitTimestamp_SetsCancelledStatusTimestampAndUserId()
    {
        // Arrange
        var plan = new ProductionPlan { Status = DomainTaskStatus.PendingApproval };
        var expertId = Guid.NewGuid();
        var rejectedAt = new DateTime(2025, 8, 12, 11, 45, 0, DateTimeKind.Utc);

        // Act
        plan.Reject(expertId, rejectedAt);

        // Assert
        plan.Status.Should().Be(DomainTaskStatus.Cancelled);
        plan.CanBeReviewed.Should().BeFalse();
        plan.IsApproved.Should().BeFalse();
        plan.ApprovedBy.Should().BeNull();
        plan.ApprovedAt.Should().BeNull();
        plan.LastModified.Should().Be(new DateTimeOffset(rejectedAt));
        plan.LastModifiedBy.Should().Be(expertId);
    }

    [Fact]
    public void ProductionPlan_Reject_WithDefaultTimestamp_SetsCurrentUtcTimestamp()
    {
        // Arrange
        var plan = new ProductionPlan { Status = DomainTaskStatus.PendingApproval };
        var expertId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        plan.Reject(expertId);
        var after = DateTime.UtcNow;

        // Assert
        plan.Status.Should().Be(DomainTaskStatus.Cancelled);
        plan.LastModified.Should().BeOnOrAfter(new DateTimeOffset(before)).And.BeOnOrBefore(new DateTimeOffset(after));
        plan.LastModifiedBy.Should().Be(expertId);
    }

    #endregion

    #region YearSeason

    [Fact]
    public void YearSeason_IsSelectionWindowOpen_EnabledAndWithinBoundsIncludingEdges_ReturnsTrue()
    {
        // Arrange
        var start = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 4, 15, 23, 59, 59, DateTimeKind.Utc);
        var yearSeason = new YearSeason
        {
            AllowFarmerSelection = true,
            FarmerSelectionWindowStart = start,
            FarmerSelectionWindowEnd = end
        };

        // Act & Assert
        yearSeason.IsSelectionWindowOpen(start).Should().BeTrue();
        yearSeason.IsSelectionWindowOpen(new DateTime(2025, 4, 8, 12, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        yearSeason.IsSelectionWindowOpen(end).Should().BeTrue();
    }

    [Fact]
    public void YearSeason_IsSelectionWindowOpen_WhenDisabled_ReturnsFalseEvenWithinDates()
    {
        // Arrange
        var yearSeason = new YearSeason
        {
            AllowFarmerSelection = false,
            FarmerSelectionWindowStart = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            FarmerSelectionWindowEnd = new DateTime(2025, 4, 15, 23, 59, 59, DateTimeKind.Utc)
        };

        // Act & Assert
        yearSeason.IsSelectionWindowOpen(new DateTime(2025, 4, 8, 12, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    [Fact]
    public void YearSeason_IsSelectionWindowOpen_OutsideBounds_ReturnsFalse()
    {
        // Arrange
        var start = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 4, 15, 23, 59, 59, DateTimeKind.Utc);
        var yearSeason = new YearSeason
        {
            AllowFarmerSelection = true,
            FarmerSelectionWindowStart = start,
            FarmerSelectionWindowEnd = end
        };

        // Act & Assert
        yearSeason.IsSelectionWindowOpen(start.AddSeconds(-1)).Should().BeFalse();
        yearSeason.IsSelectionWindowOpen(end.AddSeconds(1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void YearSeason_IsSelectionWindowOpen_MissingStartOrEndDate_ReturnsFalse(
        bool hasStart,
        bool hasEnd)
    {
        // Arrange
        var yearSeason = new YearSeason
        {
            AllowFarmerSelection = true,
            FarmerSelectionWindowStart = hasStart ? new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc) : null,
            FarmerSelectionWindowEnd = hasEnd ? new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc) : null
        };

        // Act & Assert
        yearSeason.IsSelectionWindowOpen(new DateTime(2025, 4, 8, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    [Fact]
    public void YearSeason_IsPlanningWindowOpen_WithinBoundsIncludingEdges_ReturnsTrue()
    {
        // Arrange
        var start = new DateTime(2025, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 4, 30, 23, 59, 59, DateTimeKind.Utc);
        var yearSeason = new YearSeason
        {
            PlanningWindowStart = start,
            PlanningWindowEnd = end
        };

        // Act & Assert
        yearSeason.IsPlanningWindowOpen(start).Should().BeTrue();
        yearSeason.IsPlanningWindowOpen(new DateTime(2025, 4, 22, 12, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        yearSeason.IsPlanningWindowOpen(end).Should().BeTrue();
    }

    [Fact]
    public void YearSeason_IsPlanningWindowOpen_OutsideBounds_ReturnsFalse()
    {
        // Arrange
        var start = new DateTime(2025, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 4, 30, 23, 59, 59, DateTimeKind.Utc);
        var yearSeason = new YearSeason
        {
            PlanningWindowStart = start,
            PlanningWindowEnd = end
        };

        // Act & Assert
        yearSeason.IsPlanningWindowOpen(start.AddSeconds(-1)).Should().BeFalse();
        yearSeason.IsPlanningWindowOpen(end.AddSeconds(1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void YearSeason_IsPlanningWindowOpen_MissingStartOrEndDate_ReturnsFalse(
        bool hasStart,
        bool hasEnd)
    {
        // Arrange
        var yearSeason = new YearSeason
        {
            PlanningWindowStart = hasStart ? new DateTime(2025, 4, 16, 0, 0, 0, DateTimeKind.Utc) : null,
            PlanningWindowEnd = hasEnd ? new DateTime(2025, 4, 30, 0, 0, 0, DateTimeKind.Utc) : null
        };

        // Act & Assert
        yearSeason.IsPlanningWindowOpen(new DateTime(2025, 4, 22, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    [Fact]
    public void YearSeason_IsDateWithinSeason_WithinAndOutsideBounds_ReturnsExpectedResult()
    {
        // Arrange
        var start = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 9, 30, 23, 59, 59, DateTimeKind.Utc);
        var yearSeason = new YearSeason
        {
            StartDate = start,
            EndDate = end
        };

        // Act & Assert
        yearSeason.IsDateWithinSeason(start).Should().BeTrue();
        yearSeason.IsDateWithinSeason(new DateTime(2025, 7, 15, 12, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        yearSeason.IsDateWithinSeason(end).Should().BeTrue();
        yearSeason.IsDateWithinSeason(start.AddSeconds(-1)).Should().BeFalse();
        yearSeason.IsDateWithinSeason(end.AddSeconds(1)).Should().BeFalse();
    }

    #endregion
}
