using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Application.PlotFeature.Events;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Helpers;

internal static class PlotImportHelper
{
    internal static readonly PlotImportProfile ExcelProfile = new()
    {
        RowNumberOffset = 2, // 1-based row number including Excel header row
        IncludeSeasonYearInSummary = true,
        EmptyDataError = "The uploaded Excel file is empty or invalid.",
        ValidationFailedPrefix = "Validation failed",
        FarmCodeRequiredError = row => $"Row {row}: FarmCode is required",
        FarmerNotFoundError = (row, farmCode) =>
            $"Row {row}: Farmer '{farmCode}' not found. Please import farmers first.",
        SoThuaRequiredError = row => $"Row {row}: SoThua is required and must be > 0",
        SoToRequiredError = row => $"Row {row}: SoTo is required and must be > 0",
        AreaRequiredError = row => $"Row {row}: Area is required and must be > 0",
        RiceVarietyNotFoundError = (row, variety) =>
            $"Row {row}: Rice variety '{variety}' not found. Check 'Rice_Varieties' sheet.",
        SeasonNotFoundError = (row, season, available) =>
            $"Row {row}: Season '{season}' not found. Available seasons: {available}",
        NoSeasonAvailableError = row =>
            $"Row {row}: Cannot create cultivation - no current season found and no season specified",
        YearOutOfRangeError = (row, minYear, maxYear) =>
            $"Row {row}: Year must be between {minYear} and {maxYear}",
        InvalidPolygonFormatError = row =>
            $"Row {row}: Invalid polygon format. Expected WKT POLYGON format.",
        InvalidPolygonGeometryError = row =>
            $"Row {row}: Polygon is not geometrically valid.",
        PolygonAreaMismatchError = (row, drawnHa, diffPct, registeredHa) =>
            $"Row {row}: Polygon area ({drawnHa} ha) differs by {diffPct}% from registered area ({registeredHa} ha). Maximum allowed difference is 10%.",
        PolygonParseExceptionError = (row, message) =>
            $"Row {row}: Error parsing polygon - {message}",
        InvalidCoordinateLog = "Row {RowNumber}: Invalid coordinate format. Using centroid instead.",
        CoordinateOutsideBoundaryLog = "Row {RowNumber}: Coordinate is outside polygon boundary. Using centroid instead.",
        DuplicatePlotLog = "Skipping duplicate plot: {FarmCode} Plot#{PlotNumber} (SoThua:{SoThua}, SoTo:{SoTo})",
        PlotWithPolygonLog = "Plot {FarmCode} #{PlotNumber} (SoThua:{SoThua}, SoTo:{SoTo}) imported with polygon - Status: Active",
        FarmerSelectionNotes = (seasonName, year) => $"Imported via Excel for {seasonName} {year}",
        CultivationVersionReason = (seasonName, year) => $"Created during plot import for {seasonName} {year}",
        CreatingCultivationsLog = "Creating {Count} PlotCultivation records for: {SeasonYears}",
        CreatingVersionsLog = "Creating {Count} CultivationVersion records (0)",
        PublishedEventLog = "Published PlotImportedEvent for {PlotCount} plots needing polygon drawing (out of {TotalPlots} total plots imported)",
        AllPlotsHavePolygonsLog = "All {PlotCount} imported plots have polygons - no polygon drawing tasks created",
        FoundYearSeasonLog = "Found existing YearSeason: {YearSeasonId}, AllowFarmerSelection: {AllowSelection}",
        AutoCreatingYearSeasonLog = "YearSeason not found for Season: {SeasonName}, Year: {Year}, Cluster: {ClusterId}. Auto-creating...",
        AutoCreatedYearSeasonLog = "Auto-created YearSeason: {YearSeasonId} for {SeasonName} {Year} in Cluster {ClusterId}",
        YearSeasonErrorLog = "Error getting or creating YearSeason for Season: {SeasonName}, Year: {Year}",
        AutoCreatedYearSeasonNotes = "Auto-created during plot import. Please review and configure settings.",
        BuildSuccessMessage = BuildEnglishSuccessMessage
    };

    internal static readonly PlotImportProfile DataImportProfile = new()
    {
        RowNumberOffset = 1, // 1-based index for data import (no header row)
        IncludeSeasonYearInSummary = false,
        EmptyDataError = "Không có dữ liệu thửa đất để nhập.",
        ValidationFailedPrefix = "Xác thực thất bại",
        FarmCodeRequiredError = row => $"Dòng {row}: Mã nông hộ (FarmCode) là bắt buộc",
        FarmerNotFoundError = (row, farmCode) =>
            $"Dòng {row}: Không tìm thấy nông hộ '{farmCode}'. Vui lòng nhập danh sách nông hộ trước.",
        SoThuaRequiredError = row => $"Dòng {row}: Số thửa là bắt buộc và phải > 0",
        SoToRequiredError = row => $"Dòng {row}: Số tờ là bắt buộc và phải > 0",
        AreaRequiredError = row => $"Dòng {row}: Diện tích là bắt buộc và phải > 0",
        RiceVarietyNotFoundError = (row, variety) =>
            $"Dòng {row}: Không tìm thấy giống lúa '{variety}'. Vui lòng kiểm tra trang 'Rice_Varieties'.",
        SeasonNotFoundError = (row, season, available) =>
            $"Dòng {row}: Không tìm thấy vụ mùa '{season}'. Các vụ mùa có sẵn: {available}",
        NoSeasonAvailableError = row =>
            $"Dòng {row}: Không thể tạo canh tác - không tìm thấy vụ mùa hiện tại và không có vụ mùa được chỉ định",
        YearOutOfRangeError = (row, minYear, maxYear) =>
            $"Dòng {row}: Năm phải nằm trong khoảng {minYear} và {maxYear}",
        InvalidPolygonFormatError = row =>
            $"Dòng {row}: Định dạng đa giác không hợp lệ. Yêu cầu định dạng WKT POLYGON.",
        InvalidPolygonGeometryError = row =>
            $"Dòng {row}: Đa giác không hợp lệ về mặt hình học.",
        PolygonAreaMismatchError = (row, drawnHa, diffPct, registeredHa) =>
            $"Dòng {row}: Diện tích đa giác ({drawnHa} ha) chênh lệch {diffPct}% so với diện tích đăng ký ({registeredHa} ha). Mức chênh lệch tối đa cho phép là 10%.",
        PolygonParseExceptionError = (row, message) =>
            $"Dòng {row}: Lỗi khi phân tích đa giác - {message}",
        InvalidCoordinateLog = "Dòng {RowNumber}: Định dạng tọa độ không hợp lệ. Sử dụng tâm đa giác thay thế.",
        CoordinateOutsideBoundaryLog = "Dòng {RowNumber}: Tọa độ nằm ngoài ranh giới đa giác. Sử dụng tâm đa giác thay thế.",
        DuplicatePlotLog = "Bỏ qua thửa đất trùng lặp: {FarmCode} Thửa#{PlotNumber} (Số thửa:{SoThua}, Số tờ:{SoTo})",
        PlotWithPolygonLog = "Thửa đất {FarmCode} #{PlotNumber} (Số thửa:{SoThua}, Số tờ:{SoTo}) đã được nhập với đa giác - Trạng thái: Hoạt động",
        FarmerSelectionNotes = (seasonName, year) => $"Nhập qua giao diện cho {seasonName} {year}",
        CultivationVersionReason = (seasonName, year) => $"Được tạo khi nhập thửa đất cho {seasonName} {year}",
        CreatingCultivationsLog = "Đang tạo {Count} bản ghi canh tác thửa đất",
        CreatingVersionsLog = "Đang tạo {Count} bản ghi phiên bản canh tác",
        PublishedEventLog = "Đã phát hành sự kiện PlotImportedEvent cho {PlotCount} thửa đất cần vẽ đa giác (trong tổng số {TotalPlots} thửa đất đã nhập)",
        AllPlotsHavePolygonsLog = "Tất cả {PlotCount} thửa đất đã nhập đều có đa giác - không tạo nhiệm vụ vẽ đa giác",
        FoundYearSeasonLog = "Đã tìm thấy Năm-Vụ mùa hiện có: {YearSeasonId}, Cho phép nông dân chọn: {AllowSelection}",
        AutoCreatingYearSeasonLog = "Không tìm thấy Năm-Vụ mùa cho Vụ mùa: {SeasonName}, Năm: {Year}, Cụm: {ClusterId}. Đang tự động tạo...",
        AutoCreatedYearSeasonLog = "Đã tự động tạo Năm-Vụ mùa: {YearSeasonId} cho {SeasonName} {Year} trong Cụm {ClusterId}",
        YearSeasonErrorLog = "Lỗi khi lấy hoặc tạo Năm-Vụ mùa cho Vụ mùa: {SeasonName}, Năm: {Year}",
        AutoCreatedYearSeasonNotes = "Được tạo tự động khi nhập thửa đất. Vui lòng xem xét và cấu hình cài đặt.",
        BuildSuccessMessage = BuildVietnameseSuccessMessage
    };

    internal static async Task<Result<List<PlotResponse>>> ExecuteImportAsync(
        IList<PlotImportRow>? plotImportRows,
        Guid? clusterManagerId,
        IApplicationDbContext context,
        IMediator mediator,
        ILogger logger,
        PlotImportProfile profile,
        CancellationToken cancellationToken)
    {
        if (plotImportRows == null || !plotImportRows.Any())
        {
            return Result<List<PlotResponse>>.Failure(profile.EmptyDataError);
        }

        var geometryFactory = new GeometryFactory(new PrecisionModel(), PlotGeometryHelper.Wgs84Srid);
        var wktReader = new WKTReader(geometryFactory);

        var allSeasons = await context.Seasons
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var today = DateTime.UtcNow;
        var resolvedSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, today);
        var currentSeason = resolvedSeason?.Season;
        var currentYear = resolvedSeason?.Year ?? today.Year;
        var seasonLookup = allSeasons.ToDictionary(s => s.SeasonName, s => s, StringComparer.OrdinalIgnoreCase);

        var uniqueFarmCodes = plotImportRows
            .Where(p => !string.IsNullOrWhiteSpace(p.FarmCode))
            .Select(p => p.FarmCode!)
            .Distinct()
            .ToList();

        var farmers = await context.Farmers
            .AsNoTracking()
            .Where(f => f.FarmCode != null && uniqueFarmCodes.Contains(f.FarmCode))
            .ToListAsync(cancellationToken);
        var farmerLookup = farmers.ToDictionary(f => f.FarmCode!, f => f);
        var farmerById = farmers.ToDictionary(f => f.Id, f => f);
        Guid? firstFarmerClusterId = farmers.FirstOrDefault()?.ClusterId;

        var uniqueVarietyNames = plotImportRows
            .Where(p => !string.IsNullOrWhiteSpace(p.RiceVarietyName))
            .Select(p => p.RiceVarietyName!)
            .Distinct()
            .ToList();

        var riceVarieties = await context.RiceVarieties
            .AsNoTracking()
            .Where(rv => uniqueVarietyNames.Contains(rv.VarietyName))
            .ToListAsync(cancellationToken);
        var varietyLookup = riceVarieties.ToDictionary(rv => rv.VarietyName, rv => rv);

        var validatedPolygons = new Dictionary<int, (Polygon boundary, Point coordinate)>();
        var validationErrors = new List<string>();

        for (int i = 0; i < plotImportRows.Count; i++)
        {
            ValidateRow(
                plotImportRows[i],
                i,
                rowNumber: i + profile.RowNumberOffset,
                farmerLookup,
                varietyLookup,
                seasonLookup,
                currentSeason,
                wktReader,
                validatedPolygons,
                validationErrors,
                logger,
                profile);
        }

        if (validationErrors.Any())
        {
            return Result<List<PlotResponse>>.Failure(
                $"{profile.ValidationFailedPrefix}:\n{string.Join("\n", validationErrors)}");
        }

        var yearSeasonCache = new Dictionary<(Guid seasonId, int year, Guid clusterId), YearSeason>();
        var plotList = new List<Plot>();
        var plotCultivationsToCreate = new List<PlotCultivation>();
        var cultivationVersionsToCreate = new List<CultivationVersion>();
        var plotsNeedingPolygonTasks = new List<Plot>();
        var skippedRows = 0;
        var plotsWithImportedPolygons = 0;

        for (int i = 0; i < plotImportRows.Count; i++)
        {
            var row = plotImportRows[i];

            if (IsEmptyRow(row))
            {
                skippedRows++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.FarmCode) || !farmerLookup.TryGetValue(row.FarmCode, out var farmer))
            {
                continue;
            }

            var existsInDb = await context.Plots
                .AsNoTracking()
                .AnyAsync(
                    p => p.SoThua == row.SoThua && p.SoTo == row.SoTo && p.FarmerId == farmer.Id,
                    cancellationToken);
            if (existsInDb)
            {
                logger.LogWarning(
                    profile.DuplicatePlotLog,
                    row.FarmCode, row.PlotNumber, row.SoThua, row.SoTo);
                continue;
            }

            var plotId = Guid.NewGuid();
            Polygon boundary;
            Point? coordinate = null;
            PlotStatus status;

            if (validatedPolygons.TryGetValue(i, out var polygonData))
            {
                boundary = polygonData.boundary;
                coordinate = polygonData.coordinate;
                status = PlotStatus.Active;
                plotsWithImportedPolygons++;

                logger.LogInformation(
                    profile.PlotWithPolygonLog,
                    row.FarmCode, row.PlotNumber, row.SoThua, row.SoTo);
            }
            else
            {
                // Placeholder boundary until a supervisor draws the actual polygon
                boundary = PlotGeometryHelper.CreateDefaultBoundary(geometryFactory);
                status = PlotStatus.PendingPolygon;
            }

            var newPlot = new Plot
            {
                Id = plotId,
                SoThua = row.SoThua,
                SoTo = row.SoTo,
                Area = row.Area!.Value,
                FarmerId = farmer.Id,
                SoilType = row.SoilType,
                Status = status,
                Boundary = boundary,
                Coordinate = coordinate
            };

            plotList.Add(newPlot);

            if (status == PlotStatus.PendingPolygon)
            {
                plotsNeedingPolygonTasks.Add(newPlot);
            }

            if (string.IsNullOrWhiteSpace(row.RiceVarietyName) ||
                !varietyLookup.TryGetValue(row.RiceVarietyName, out var riceVariety))
            {
                continue;
            }

            var seasonToUse = !string.IsNullOrWhiteSpace(row.SeasonName) &&
                              seasonLookup.TryGetValue(row.SeasonName, out var specifiedSeason)
                ? specifiedSeason
                : currentSeason;

            if (seasonToUse == null)
            {
                continue;
            }

            var cultivationYear = row.Year ?? currentYear;
            var yearSeasonForCultivation = firstFarmerClusterId.HasValue
                ? await GetOrCacheYearSeasonAsync(
                    context,
                    yearSeasonCache,
                    seasonToUse,
                    cultivationYear,
                    firstFarmerClusterId.Value,
                    logger,
                    profile,
                    cancellationToken)
                : null;

            var allowFarmerSelection = yearSeasonForCultivation?.AllowFarmerSelection == true;
            var plotCultivationId = Guid.NewGuid();
            plotCultivationsToCreate.Add(new PlotCultivation
            {
                Id = plotCultivationId,
                PlotId = plotId,
                SeasonId = seasonToUse.Id,
                RiceVarietyId = riceVariety.Id,
                PlantingDate = row.PlantingDate ?? DateTime.UtcNow,
                Area = row.Area.Value,
                Status = CultivationStatus.Planned,
                YearSeasonId = yearSeasonForCultivation?.Id,
                IsFarmerConfirmed = allowFarmerSelection,
                FarmerSelectionDate = allowFarmerSelection ? DateTime.UtcNow : null,
                FarmerSelectionNotes = allowFarmerSelection
                    ? profile.FarmerSelectionNotes(seasonToUse.SeasonName, cultivationYear)
                    : null
            });

            cultivationVersionsToCreate.Add(new CultivationVersion
            {
                PlotCultivationId = plotCultivationId,
                VersionName = "0",
                VersionOrder = 1,
                IsActive = true,
                Reason = profile.CultivationVersionReason(seasonToUse.SeasonName, cultivationYear),
                ActivatedAt = DateTime.UtcNow
            });
        }

        if (plotList.Any())
        {
            context.Plots.AddRange(plotList);
        }

        string? seasonYearDisplay = null;
        if (plotCultivationsToCreate.Any())
        {
            context.PlotCultivations.AddRange(plotCultivationsToCreate);

            if (profile.IncludeSeasonYearInSummary)
            {
                seasonYearDisplay = FormatSeasonYearDisplay(
                    plotCultivationsToCreate,
                    yearSeasonCache,
                    allSeasons,
                    currentYear);

                logger.LogInformation(
                    profile.CreatingCultivationsLog,
                    plotCultivationsToCreate.Count,
                    seasonYearDisplay);
            }
            else
            {
                logger.LogInformation(
                    profile.CreatingCultivationsLog,
                    plotCultivationsToCreate.Count);
            }
        }

        if (cultivationVersionsToCreate.Any())
        {
            context.CultivationVersions.AddRange(cultivationVersionsToCreate);
            logger.LogInformation(profile.CreatingVersionsLog, cultivationVersionsToCreate.Count);
        }

        await context.SaveChangesAsync(cancellationToken);

        var plotCreateSuccessList = plotList.Select(plot => new PlotResponse
        {
            PlotId = plot.Id,
            SoThua = plot.SoThua,
            SoTo = plot.SoTo,
            Area = plot.Area,
            FarmerId = plot.FarmerId,
            FarmerName = farmerById.TryGetValue(plot.FarmerId, out var farmer) ? farmer.FullName ?? string.Empty : string.Empty,
            SoilType = plot.SoilType,
            Status = plot.Status,
            GroupId = plot.GroupPlots.FirstOrDefault()?.GroupId
        }).ToList();

        if (plotsNeedingPolygonTasks.Any())
        {
            var pendingPlotIds = plotsNeedingPolygonTasks.Select(p => p.Id).ToHashSet();
            var plotsNeedingTasksResponse = plotCreateSuccessList
                .Where(pr => pendingPlotIds.Contains(pr.PlotId))
                .ToList();

            await mediator.Publish(new PlotImportedEvent
            {
                ImportedPlots = plotsNeedingTasksResponse,
                ClusterManagerId = clusterManagerId,
                ImportedAt = DateTime.UtcNow,
                TotalPlotsImported = plotsNeedingTasksResponse.Count
            }, cancellationToken);

            logger.LogInformation(
                profile.PublishedEventLog,
                plotsNeedingTasksResponse.Count,
                plotCreateSuccessList.Count);
        }
        else
        {
            logger.LogInformation(profile.AllPlotsHavePolygonsLog, plotCreateSuccessList.Count);
        }

        var message = profile.BuildSuccessMessage(
            plotCreateSuccessList.Count,
            plotsWithImportedPolygons,
            plotsNeedingPolygonTasks.Count,
            plotCultivationsToCreate.Count,
            cultivationVersionsToCreate.Count,
            seasonYearDisplay,
            skippedRows);

        return Result<List<PlotResponse>>.Success(plotCreateSuccessList, message);
    }

    private static bool IsEmptyRow(PlotImportRow row) =>
        !row.SoThua.HasValue && !row.SoTo.HasValue && !row.Area.HasValue;

    private static void ValidateRow(
        PlotImportRow row,
        int rowIndex,
        int rowNumber,
        Dictionary<string, Farmer> farmerLookup,
        Dictionary<string, RiceVariety> varietyLookup,
        Dictionary<string, Season> seasonLookup,
        Season? currentSeason,
        WKTReader wktReader,
        Dictionary<int, (Polygon boundary, Point coordinate)> validatedPolygons,
        List<string> validationErrors,
        ILogger logger,
        PlotImportProfile profile)
    {
        if (IsEmptyRow(row))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(row.FarmCode))
        {
            validationErrors.Add(profile.FarmCodeRequiredError(rowNumber));
            return;
        }

        if (!farmerLookup.ContainsKey(row.FarmCode))
        {
            validationErrors.Add(profile.FarmerNotFoundError(rowNumber, row.FarmCode));
            return;
        }

        if (!row.SoThua.HasValue || row.SoThua.Value <= 0)
        {
            validationErrors.Add(profile.SoThuaRequiredError(rowNumber));
        }

        if (!row.SoTo.HasValue || row.SoTo.Value <= 0)
        {
            validationErrors.Add(profile.SoToRequiredError(rowNumber));
        }

        if (!row.Area.HasValue || row.Area.Value <= 0)
        {
            validationErrors.Add(profile.AreaRequiredError(rowNumber));
        }

        if (!string.IsNullOrWhiteSpace(row.RiceVarietyName))
        {
            if (!varietyLookup.ContainsKey(row.RiceVarietyName))
            {
                validationErrors.Add(profile.RiceVarietyNotFoundError(rowNumber, row.RiceVarietyName));
            }

            if (!string.IsNullOrWhiteSpace(row.SeasonName))
            {
                if (!seasonLookup.ContainsKey(row.SeasonName))
                {
                    var availableSeasons = string.Join(", ", seasonLookup.Keys);
                    validationErrors.Add(profile.SeasonNotFoundError(rowNumber, row.SeasonName, availableSeasons));
                }
            }
            else if (currentSeason == null)
            {
                validationErrors.Add(profile.NoSeasonAvailableError(rowNumber));
            }

            if (row.Year.HasValue)
            {
                var minYear = DateTime.Now.Year - 5;
                var maxYear = DateTime.Now.Year + 5;

                if (row.Year.Value < minYear || row.Year.Value > maxYear)
                {
                    validationErrors.Add(profile.YearOutOfRangeError(rowNumber, minYear, maxYear));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(row.BoundaryWKT) &&
            TryValidateAndParsePolygon(row, rowNumber, wktReader, validationErrors, logger, profile, out var polygonData))
        {
            validatedPolygons[rowIndex] = polygonData;
        }
    }

    private static bool TryValidateAndParsePolygon(
        PlotImportRow row,
        int rowNumber,
        WKTReader wktReader,
        List<string> validationErrors,
        ILogger logger,
        PlotImportProfile profile,
        out (Polygon boundary, Point coordinate) polygonData)
    {
        polygonData = default;

        try
        {
            var boundary = wktReader.Read(row.BoundaryWKT) as Polygon;
            if (boundary == null)
            {
                validationErrors.Add(profile.InvalidPolygonFormatError(rowNumber));
                return false;
            }

            if (!boundary.IsValid)
            {
                validationErrors.Add(profile.InvalidPolygonGeometryError(rowNumber));
                return false;
            }

            boundary.SRID = PlotGeometryHelper.Wgs84Srid;

            // Validate polygon area is within 10% tolerance of registered area
            if (row.Area.HasValue)
            {
                var drawnAreaHa = PlotGeometryHelper.CalculateAreaInHectares(boundary, decimalPlaces: 2);
                var registeredArea = row.Area.Value;
                var differencePercent = Math.Abs((drawnAreaHa - registeredArea) / registeredArea * 100);

                if (differencePercent > 10)
                {
                    validationErrors.Add(profile.PolygonAreaMismatchError(
                        rowNumber,
                        Math.Round(drawnAreaHa, 2),
                        Math.Round(differencePercent, 2),
                        Math.Round(registeredArea, 2)));
                    return false;
                }
            }

            var coordinate = ParseCoordinateOrCentroid(row.CoordinateWKT, boundary, rowNumber, wktReader, logger, profile);
            polygonData = (boundary, coordinate);
            return true;
        }
        catch (Exception ex)
        {
            validationErrors.Add(profile.PolygonParseExceptionError(rowNumber, ex.Message));
            return false;
        }
    }

    private static Point ParseCoordinateOrCentroid(
        string? coordinateWkt,
        Polygon boundary,
        int rowNumber,
        WKTReader wktReader,
        ILogger logger,
        PlotImportProfile profile)
    {
        if (string.IsNullOrWhiteSpace(coordinateWkt))
        {
            var centroid = boundary.Centroid;
            centroid.SRID = PlotGeometryHelper.Wgs84Srid;
            return centroid;
        }

        var coordinate = wktReader.Read(coordinateWkt) as Point;
        if (coordinate == null)
        {
            logger.LogWarning(profile.InvalidCoordinateLog, rowNumber);
            var centroid = boundary.Centroid;
            centroid.SRID = PlotGeometryHelper.Wgs84Srid;
            return centroid;
        }

        if (!boundary.Contains(coordinate))
        {
            logger.LogWarning(profile.CoordinateOutsideBoundaryLog, rowNumber);
            var centroid = boundary.Centroid;
            centroid.SRID = PlotGeometryHelper.Wgs84Srid;
            return centroid;
        }

        coordinate.SRID = PlotGeometryHelper.Wgs84Srid;
        return coordinate;
    }

    private static async Task<YearSeason?> GetOrCacheYearSeasonAsync(
        IApplicationDbContext context,
        Dictionary<(Guid seasonId, int year, Guid clusterId), YearSeason> yearSeasonCache,
        Season season,
        int year,
        Guid clusterId,
        ILogger logger,
        PlotImportProfile profile,
        CancellationToken cancellationToken)
    {
        var cacheKey = (season.Id, year, clusterId);
        if (yearSeasonCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var yearSeason = await GetOrCreateYearSeasonAsync(
            context, season, year, clusterId, logger, profile, cancellationToken);
        if (yearSeason != null)
        {
            yearSeasonCache[cacheKey] = yearSeason;
        }

        return yearSeason;
    }

    private static async Task<YearSeason?> GetOrCreateYearSeasonAsync(
        IApplicationDbContext context,
        Season season,
        int year,
        Guid clusterId,
        ILogger logger,
        PlotImportProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            var yearSeason = await context.YearSeasons
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    ys => ys.SeasonId == season.Id && ys.Year == year && ys.ClusterId == clusterId,
                    cancellationToken);

            if (yearSeason != null)
            {
                logger.LogInformation(
                    profile.FoundYearSeasonLog,
                    yearSeason.Id,
                    yearSeason.AllowFarmerSelection);
                return yearSeason;
            }

            logger.LogInformation(
                profile.AutoCreatingYearSeasonLog,
                season.SeasonName,
                year,
                clusterId);

            var seasonStart = ParseSeasonDate(season.StartDate, year);
            var seasonEnd = ParseSeasonDate(season.EndDate, year);

            // Handle year wraparound (e.g., Winter-Spring spanning across calendar years)
            if (seasonEnd < seasonStart)
            {
                seasonEnd = seasonEnd.AddYears(1);
            }

            yearSeason = new YearSeason
            {
                SeasonId = season.Id,
                ClusterId = clusterId,
                Year = year,
                RiceVarietyId = null,
                AllowFarmerSelection = true,
                FarmerSelectionWindowStart = DateTime.UtcNow,
                FarmerSelectionWindowEnd = seasonStart.AddDays(-7),
                StartDate = seasonStart,
                EndDate = seasonEnd,
                Status = SeasonStatus.Draft,
                Notes = profile.AutoCreatedYearSeasonNotes,
                AllowedPlantingFlexibilityDays = 7,
                PlanningWindowStart = DateTime.UtcNow,
                PlanningWindowEnd = seasonStart.AddDays(-3)
            };

            context.YearSeasons.Add(yearSeason);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                profile.AutoCreatedYearSeasonLog,
                yearSeason.Id,
                season.SeasonName,
                year,
                clusterId);

            return yearSeason;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, profile.YearSeasonErrorLog, season.SeasonName, year);
            return null;
        }
    }

    private static DateTime ParseSeasonDate(string dateStr, int year)
    {
        if (SeasonDateHelper.TryParseMonthDay(dateStr, out var month, out var day))
        {
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }

        var parts = dateStr.Split('/');
        var fallbackDay = int.Parse(parts[0]);
        var fallbackMonth = int.Parse(parts[1]);
        return new DateTime(year, fallbackMonth, fallbackDay, 0, 0, 0, DateTimeKind.Utc);
    }

    private static string FormatSeasonYearDisplay(
        List<PlotCultivation> cultivations,
        Dictionary<(Guid seasonId, int year, Guid clusterId), YearSeason> yearSeasonCache,
        IReadOnlyList<Season> allSeasons,
        int currentYear)
    {
        var seasonYearCombos = cultivations
            .Select(pc =>
            {
                var ys = yearSeasonCache.Values.FirstOrDefault(y => y.Id == pc.YearSeasonId);
                var season = allSeasons.FirstOrDefault(s => s.Id == pc.SeasonId);
                var year = ys?.Year ?? currentYear;
                return $"{season?.SeasonName} {year}";
            })
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        return seasonYearCombos.Count == 1
            ? seasonYearCombos[0]
            : string.Join(", ", seasonYearCombos);
    }

    private static string BuildEnglishSuccessMessage(
        int totalPlots,
        int plotsWithImportedPolygons,
        int plotsNeedingPolygonCount,
        int cultivationCount,
        int versionCount,
        string? seasonYearDisplay,
        int skippedRows)
    {
        var message = $"Successfully imported {totalPlots} plots";
        if (plotsWithImportedPolygons > 0)
        {
            message += $" ({plotsWithImportedPolygons} with polygons, {plotsNeedingPolygonCount} pending polygon drawing)";
        }

        if (cultivationCount > 0)
        {
            message += $" with {cultivationCount} cultivation records";
            if (versionCount > 0)
            {
                message += $" and {versionCount} initial versions";
            }
            message += $" for {seasonYearDisplay}";
        }

        if (skippedRows > 0)
        {
            message += $" ({skippedRows} empty rows skipped)";
        }

        return message;
    }

    private static string BuildVietnameseSuccessMessage(
        int totalPlots,
        int plotsWithImportedPolygons,
        int plotsNeedingPolygonCount,
        int cultivationCount,
        int versionCount,
        string? seasonYearDisplay,
        int skippedRows)
    {
        var message = $"Đã nhập thành công {totalPlots} thửa đất";
        if (plotsWithImportedPolygons > 0)
        {
            message += $" ({plotsWithImportedPolygons} có đa giác, {plotsNeedingPolygonCount} đang chờ vẽ đa giác)";
        }

        if (cultivationCount > 0)
        {
            message += $" với {cultivationCount} bản ghi canh tác";
            if (versionCount > 0)
            {
                message += $" và {versionCount} phiên bản ban đầu";
            }
        }

        if (skippedRows > 0)
        {
            message += $" ({skippedRows} dòng trống đã bỏ qua)";
        }

        return message;
    }

    internal sealed class PlotImportProfile
    {
        public required int RowNumberOffset { get; init; }
        public required bool IncludeSeasonYearInSummary { get; init; }
        public required string EmptyDataError { get; init; }
        public required string ValidationFailedPrefix { get; init; }
        public required Func<int, string> FarmCodeRequiredError { get; init; }
        public required Func<int, string, string> FarmerNotFoundError { get; init; }
        public required Func<int, string> SoThuaRequiredError { get; init; }
        public required Func<int, string> SoToRequiredError { get; init; }
        public required Func<int, string> AreaRequiredError { get; init; }
        public required Func<int, string, string> RiceVarietyNotFoundError { get; init; }
        public required Func<int, string, string, string> SeasonNotFoundError { get; init; }
        public required Func<int, string> NoSeasonAvailableError { get; init; }
        public required Func<int, int, int, string> YearOutOfRangeError { get; init; }
        public required Func<int, string> InvalidPolygonFormatError { get; init; }
        public required Func<int, string> InvalidPolygonGeometryError { get; init; }
        public required Func<int, decimal, decimal, decimal, string> PolygonAreaMismatchError { get; init; }
        public required Func<int, string, string> PolygonParseExceptionError { get; init; }
        public required string InvalidCoordinateLog { get; init; }
        public required string CoordinateOutsideBoundaryLog { get; init; }
        public required string DuplicatePlotLog { get; init; }
        public required string PlotWithPolygonLog { get; init; }
        public required Func<string, int, string> FarmerSelectionNotes { get; init; }
        public required Func<string, int, string> CultivationVersionReason { get; init; }
        public required string CreatingCultivationsLog { get; init; }
        public required string CreatingVersionsLog { get; init; }
        public required string PublishedEventLog { get; init; }
        public required string AllPlotsHavePolygonsLog { get; init; }
        public required string FoundYearSeasonLog { get; init; }
        public required string AutoCreatingYearSeasonLog { get; init; }
        public required string AutoCreatedYearSeasonLog { get; init; }
        public required string YearSeasonErrorLog { get; init; }
        public required string AutoCreatedYearSeasonNotes { get; init; }
        public required Func<int, int, int, int, int, string?, int, string> BuildSuccessMessage { get; init; }
    }
}
