using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
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

namespace RiceProduction.Application.PlotFeature.Commands.ImportFromData;

public class ImportPlotsFromDataCommandHandler 
    : IRequestHandler<ImportPlotsFromDataCommand, Result<List<PlotResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<ImportPlotsFromDataCommandHandler> _logger;

    public ImportPlotsFromDataCommandHandler(
        IUnitOfWork unitOfWork, 
        IMediator mediator, 
        ILogger<ImportPlotsFromDataCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<List<PlotResponse>>> Handle(
        ImportPlotsFromDataCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var plotImportRows = request.PlotRows;
            if (plotImportRows == null || !plotImportRows.Any())
            {
                return Result<List<PlotResponse>>.Failure(
                    "Không có dữ liệu thửa đất để nhập.");
            }

            var plotRepo = _unitOfWork.Repository<Plot>();
            var farmerRepo = _unitOfWork.FarmerRepository;
            var riceVarietyRepo = _unitOfWork.Repository<RiceVariety>();

            // Initialize WKT reader for polygon parsing
            var wktReader = new WKTReader(new GeometryFactory(new PrecisionModel(), 4326));

            // Get current season and year (fallback values)
            var (currentSeason, currentYear) = await GetCurrentSeasonAndYear(cancellationToken);
            
            // Cache all seasons by name for lookup
            var allSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
            var seasonLookup = allSeasons.ToDictionary(s => s.SeasonName, s => s, StringComparer.OrdinalIgnoreCase);
            
            // Cache farmers by FarmCode
            var uniqueFarmCodes = plotImportRows
                .Where(p => !string.IsNullOrWhiteSpace(p.FarmCode))
                .Select(p => p.FarmCode)
                .Distinct()
                .ToList();
                
            var farmers = await farmerRepo.ListAsync(f => uniqueFarmCodes.Contains(f.FarmCode));
            var farmerLookup = farmers.ToDictionary(f => f.FarmCode, f => f);
            
            // Get ClusterId from first farmer for YearSeason lookup
            Guid? firstFarmerClusterId = null;
            if (farmers.Any())
            {
                firstFarmerClusterId = farmers.First().ClusterId;
            }
            
            // Cache for YearSeasons by (SeasonId, Year, ClusterId) key
            var yearSeasonCache = new Dictionary<(Guid seasonId, int year, Guid clusterId), YearSeason>();
            
            // Cache rice varieties by name
            var uniqueVarietyNames = plotImportRows
                .Where(p => !string.IsNullOrWhiteSpace(p.RiceVarietyName))
                .Select(p => p.RiceVarietyName)
                .Distinct()
                .ToList();
                
            var riceVarieties = await riceVarietyRepo.ListAsync(
                rv => uniqueVarietyNames.Contains(rv.VarietyName));
            var varietyLookup = riceVarieties.ToDictionary(rv => rv.VarietyName, rv => rv);

            // Dictionary to store validated polygons per row index
            var validatedPolygons = new Dictionary<int, (Polygon boundary, Point coordinate)>();

            // Validate
            var validationErrors = new List<string>();
            for (int i = 0; i < plotImportRows.Count; i++)
            {
                var row = plotImportRows[i];
                var rowNumber = i + 1; // Use 1-based index for data import (no header row)

                // Skip rows with no plot data
                if (!row.SoThua.HasValue && !row.SoTo.HasValue && !row.Area.HasValue)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.FarmCode))
                {
                    validationErrors.Add($"Dòng {rowNumber}: Mã nông hộ (FarmCode) là bắt buộc");
                    continue;
                }

                if (!farmerLookup.ContainsKey(row.FarmCode))
                {
                    validationErrors.Add(
                        $"Dòng {rowNumber}: Không tìm thấy nông hộ '{row.FarmCode}'. Vui lòng nhập danh sách nông hộ trước.");
                    continue;
                }

                if (!row.SoThua.HasValue || row.SoThua.Value <= 0)
                {
                    validationErrors.Add($"Dòng {rowNumber}: Số thửa là bắt buộc và phải > 0");
                }

                if (!row.SoTo.HasValue || row.SoTo.Value <= 0)
                {
                    validationErrors.Add($"Dòng {rowNumber}: Số tờ là bắt buộc và phải > 0");
                }

                if (!row.Area.HasValue || row.Area.Value <= 0)
                {
                    validationErrors.Add($"Dòng {rowNumber}: Diện tích là bắt buộc và phải > 0");
                }

                // Validate rice variety if provided
                if (!string.IsNullOrWhiteSpace(row.RiceVarietyName))
                {
                    if (!varietyLookup.ContainsKey(row.RiceVarietyName))
                    {
                        validationErrors.Add(
                            $"Dòng {rowNumber}: Không tìm thấy giống lúa '{row.RiceVarietyName}'. Vui lòng kiểm tra trang 'Rice_Varieties'.");
                    }

                    // Validate season name if provided
                    if (!string.IsNullOrWhiteSpace(row.SeasonName))
                    {
                        if (!seasonLookup.ContainsKey(row.SeasonName))
                        {
                            var availableSeasons = string.Join(", ", seasonLookup.Keys);
                            validationErrors.Add(
                                $"Dòng {rowNumber}: Không tìm thấy vụ mùa '{row.SeasonName}'. Các vụ mùa có sẵn: {availableSeasons}");
                        }
                    }
                    else if (currentSeason == null)
                    {
                        validationErrors.Add(
                            $"Dòng {rowNumber}: Không thể tạo canh tác - không tìm thấy vụ mùa hiện tại và không có vụ mùa được chỉ định");
                    }
                    
                    // Validate year if provided
                    if (row.Year.HasValue)
                    {
                        var minYear = DateTime.Now.Year - 5;
                        var maxYear = DateTime.Now.Year + 5;
                        
                        if (row.Year.Value < minYear || row.Year.Value > maxYear)
                        {
                            validationErrors.Add(
                                $"Dòng {rowNumber}: Năm phải nằm trong khoảng {minYear} và {maxYear}");
                        }
                    }
                }

                // Validate polygon if provided
                if (!string.IsNullOrWhiteSpace(row.BoundaryWKT))
                {
                    try
                    {
                        var boundary = wktReader.Read(row.BoundaryWKT) as Polygon;
                        
                        if (boundary == null)
                        {
                            validationErrors.Add(
                                $"Dòng {rowNumber}: Định dạng đa giác không hợp lệ. Yêu cầu định dạng WKT POLYGON.");
                            continue;
                        }

                        if (!boundary.IsValid)
                        {
                            validationErrors.Add(
                                $"Dòng {rowNumber}: Đa giác không hợp lệ về mặt hình học.");
                            continue;
                        }

                        boundary.SRID = 4326;

                        // Validate area matches (10% tolerance)
                        if (row.Area.HasValue)
                        {
                            var drawnAreaHa = CalculateAreaInHectares(boundary);
                            var registeredArea = row.Area.Value;
                            var differencePercent = Math.Abs((drawnAreaHa - registeredArea) / registeredArea * 100);

                            if (differencePercent > 10)
                            {
                                validationErrors.Add(
                                    $"Dòng {rowNumber}: Diện tích đa giác ({Math.Round(drawnAreaHa, 2)} ha) chênh lệch {Math.Round(differencePercent, 2)}% so với diện tích đăng ký ({Math.Round(registeredArea, 2)} ha). Mức chênh lệch tối đa cho phép là 10%.");
                                continue;
                            }
                        }

                        // Parse coordinate or use centroid
                        Point coordinate;
                        if (!string.IsNullOrWhiteSpace(row.CoordinateWKT))
                        {
                            coordinate = wktReader.Read(row.CoordinateWKT) as Point;
                            if (coordinate == null)
                            {
                                _logger.LogWarning(
                                    "Dòng {RowNumber}: Định dạng tọa độ không hợp lệ. Sử dụng tâm đa giác thay thế.", 
                                    rowNumber);
                                coordinate = boundary.Centroid;
                            }
                            else if (!boundary.Contains(coordinate))
                            {
                                _logger.LogWarning(
                                    "Dòng {RowNumber}: Tọa độ nằm ngoài ranh giới đa giác. Sử dụng tâm đa giác thay thế.", 
                                    rowNumber);
                                coordinate = boundary.Centroid;
                            }
                            else
                            {
                                coordinate.SRID = 4326;
                            }
                        }
                        else
                        {
                            coordinate = boundary.Centroid;
                            coordinate.SRID = 4326;
                        }

                        // Store validated polygon for this row
                        validatedPolygons[i] = (boundary, coordinate);
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add(
                            $"Dòng {rowNumber}: Lỗi khi phân tích đa giác - {ex.Message}");
                    }
                }
            }

            if (validationErrors.Any())
            {
                return Result<List<PlotResponse>>.Failure(
                    $"Xác thực thất bại:\n{string.Join("\n", validationErrors)}");
            }

            // Process plots (same logic as Excel import)
            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var plotList = new List<Plot>();
            var plotCultivationsToCreate = new List<PlotCultivation>();
            var cultivationVersionsToCreate = new List<CultivationVersion>();
            var plotsNeedingPolygonTasks = new List<Plot>();
            var skippedRows = 0;
            var plotsWithImportedPolygons = 0;

            foreach (var row in plotImportRows)
            {
                // Skip empty rows
                if (!row.SoThua.HasValue && !row.SoTo.HasValue && !row.Area.HasValue)
                {
                    skippedRows++;
                    continue;
                }

                if (!farmerLookup.TryGetValue(row.FarmCode, out var farmer))
                {
                    continue;
                }

                // Check for duplicates
                var existingPlot = await plotRepo.FindAsync(p => 
                    p.SoThua == row.SoThua && 
                    p.SoTo == row.SoTo && 
                    p.FarmerId == farmer.Id);
                    
                if (existingPlot != null)
                {
                    _logger.LogWarning(
                        "Bỏ qua thửa đất trùng lặp: {FarmCode} Thửa#{PlotNumber} (Số thửa:{SoThua}, Số tờ:{SoTo})",
                        row.FarmCode, row.PlotNumber, row.SoThua, row.SoTo);
                    continue;
                }

                var plotId = await plotRepo.GenerateNewGuid(Guid.NewGuid());
                
                // Determine boundary and status based on whether polygon was imported
                Polygon boundary;
                Point? coordinate = null;
                PlotStatus status;
                var rowIndex = plotImportRows.IndexOf(row);
                
                if (validatedPolygons.TryGetValue(rowIndex, out var polygonData))
                {
                    // Use imported polygon
                    boundary = polygonData.boundary;
                    coordinate = polygonData.coordinate;
                    status = PlotStatus.Active;
                    plotsWithImportedPolygons++;
                    
                    _logger.LogInformation(
                        "Thửa đất {FarmCode} #{PlotNumber} (Số thửa:{SoThua}, Số tờ:{SoTo}) đã được nhập với đa giác - Trạng thái: Hoạt động",
                        row.FarmCode, row.PlotNumber, row.SoThua, row.SoTo);
                }
                else
                {
                    // Use dummy boundary - will need supervisor to draw
                    var coordinates = new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(0, 0.001),
                        new Coordinate(0.001, 0.001),
                        new Coordinate(0.001, 0),
                        new Coordinate(0, 0)
                    };
                    boundary = geometryFactory.CreatePolygon(coordinates);
                    status = PlotStatus.PendingPolygon;
                }

                var newPlot = new Plot
                {
                    Id = plotId,
                    SoThua = row.SoThua,
                    SoTo = row.SoTo,
                    Area = row.Area.Value,
                    FarmerId = farmer.Id,
                    SoilType = row.SoilType,
                    Status = status,
                    Boundary = boundary,
                    Coordinate = coordinate
                };

                plotList.Add(newPlot);
                
                // Only add to polygon task list if no polygon was imported
                if (status == PlotStatus.PendingPolygon)
                {
                    plotsNeedingPolygonTasks.Add(newPlot);
                }

                // Create PlotCultivation if rice variety specified
                if (!string.IsNullOrWhiteSpace(row.RiceVarietyName) && 
                    varietyLookup.TryGetValue(row.RiceVarietyName, out var riceVariety))
                {
                    // Determine which season to use: specified or current
                    Season? seasonToUse = null;
                    if (!string.IsNullOrWhiteSpace(row.SeasonName) && seasonLookup.TryGetValue(row.SeasonName, out var specifiedSeason))
                    {
                        seasonToUse = specifiedSeason;
                    }
                    else
                    {
                        seasonToUse = currentSeason;
                    }
                    
                    // Skip if no valid season
                    if (seasonToUse == null)
                    {
                        continue;
                    }
                    
                    // Use provided year or fall back to current year
                    var cultivationYear = row.Year ?? currentYear;
                    
                    // Get or create YearSeason for this specific season, year and cluster
                    YearSeason? yearSeasonForCultivation = null;
                    if (firstFarmerClusterId.HasValue)
                    {
                        var cacheKey = (seasonToUse.Id, cultivationYear, firstFarmerClusterId.Value);
                        
                        if (!yearSeasonCache.TryGetValue(cacheKey, out yearSeasonForCultivation))
                        {
                            yearSeasonForCultivation = await GetOrCreateYearSeasonAsync(
                                seasonToUse,
                                cultivationYear,
                                firstFarmerClusterId.Value,
                                cancellationToken);
                                
                            if (yearSeasonForCultivation != null)
                            {
                                yearSeasonCache[cacheKey] = yearSeasonForCultivation;
                            }
                        }
                    }
                    
                    var plotCultivationId = Guid.NewGuid();
                    var plotCultivation = new PlotCultivation
                    {
                        Id = plotCultivationId,
                        PlotId = plotId,
                        SeasonId = seasonToUse.Id,
                        RiceVarietyId = riceVariety.Id,
                        PlantingDate = row.PlantingDate ?? DateTime.UtcNow,
                        Area = row.Area.Value,
                        Status = CultivationStatus.Planned,
                        
                        // Farmer selection fields
                        YearSeasonId = yearSeasonForCultivation?.Id,
                        IsFarmerConfirmed = yearSeasonForCultivation?.AllowFarmerSelection == true,
                        FarmerSelectionDate = yearSeasonForCultivation?.AllowFarmerSelection == true 
                            ? DateTime.UtcNow 
                            : null,
                        FarmerSelectionNotes = yearSeasonForCultivation?.AllowFarmerSelection == true 
                            ? $"Nhập qua giao diện cho {seasonToUse.SeasonName} {cultivationYear}" 
                            : null
                    };

                    plotCultivationsToCreate.Add(plotCultivation);

                    // Create first version for this PlotCultivation
                    var firstVersion = new CultivationVersion
                    {
                        PlotCultivationId = plotCultivationId,
                        VersionName = "0",
                        VersionOrder = 1,
                        IsActive = true,
                        Reason = $"Được tạo khi nhập thửa đất cho {seasonToUse.SeasonName} {cultivationYear}",
                        ActivatedAt = DateTime.UtcNow
                    };

                    cultivationVersionsToCreate.Add(firstVersion);
                }
            }

            // Save everything
            if (plotList.Any())
            {
                await plotRepo.AddRangeAsync(plotList);
            }

            if (plotCultivationsToCreate.Any())
            {
                var plotCultivationRepo = _unitOfWork.Repository<PlotCultivation>();
                await plotCultivationRepo.AddRangeAsync(plotCultivationsToCreate);
                
                    _logger.LogInformation(
                        "Đang tạo {Count} bản ghi canh tác thửa đất",
                        plotCultivationsToCreate.Count);
                }

                if (cultivationVersionsToCreate.Any())
                {
                    var cultivationVersionRepo = _unitOfWork.Repository<CultivationVersion>();
                    await cultivationVersionRepo.AddRangeAsync(cultivationVersionsToCreate);
                    
                    _logger.LogInformation(
                        "Đang tạo {Count} bản ghi phiên bản canh tác",
                        cultivationVersionsToCreate.Count);
                }

            await _unitOfWork.CompleteAsync();

            // Create response
            var plotCreateSuccessList = new List<PlotResponse>();
            foreach (var plot in plotList)
            {
                var farmer = await farmerRepo.GetFarmerByIdAsync(plot.FarmerId, cancellationToken);
                plotCreateSuccessList.Add(new PlotResponse
                {
                    PlotId = plot.Id,
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    Area = plot.Area,
                    FarmerId = plot.FarmerId,
                    FarmerName = farmer?.FullName ?? string.Empty,
                    SoilType = plot.SoilType,
                    Status = plot.Status,
                    GroupId = plot.GroupPlots.FirstOrDefault()?.GroupId
                });
            }

            // Publish event for polygon assignment
            if (plotsNeedingPolygonTasks.Any())
            {
                var plotsNeedingTasksResponse = plotCreateSuccessList
                    .Where(pr => plotsNeedingPolygonTasks.Any(p => p.Id == pr.PlotId))
                    .ToList();
                    
                await _mediator.Publish(new PlotImportedEvent
                {
                    ImportedPlots = plotsNeedingTasksResponse,
                    ClusterManagerId = request.ClusterManagerId,
                    ImportedAt = DateTime.UtcNow,
                    TotalPlotsImported = plotsNeedingTasksResponse.Count
                }, cancellationToken);
                
                _logger.LogInformation(
                    "Đã phát hành sự kiện PlotImportedEvent cho {PlotCount} thửa đất cần vẽ đa giác (trong tổng số {TotalPlots} thửa đất đã nhập)",
                    plotsNeedingTasksResponse.Count,
                    plotCreateSuccessList.Count);
            }
            else
            {
                _logger.LogInformation(
                    "Tất cả {PlotCount} thửa đất đã nhập đều có đa giác - không tạo nhiệm vụ vẽ đa giác",
                    plotCreateSuccessList.Count);
            }

            var message = $"Đã nhập thành công {plotCreateSuccessList.Count} thửa đất";
            if (plotsWithImportedPolygons > 0)
            {
                message += $" ({plotsWithImportedPolygons} có đa giác, {plotsNeedingPolygonTasks.Count} đang chờ vẽ đa giác)";
            }
            if (plotCultivationsToCreate.Any())
            {
                message += $" với {plotCultivationsToCreate.Count} bản ghi canh tác";
                if (cultivationVersionsToCreate.Any())
                {
                    message += $" và {cultivationVersionsToCreate.Count} phiên bản ban đầu";
                }
            }
            if (skippedRows > 0)
            {
                message += $" ({skippedRows} dòng trống đã bỏ qua)";
            }

            return Result<List<PlotResponse>>.Success(plotCreateSuccessList, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi nhập thửa đất từ dữ liệu");
            return Result<List<PlotResponse>>.Failure(
                $"Nhập thất bại: {ex.Message}");
        }
    }

    private async Task<(Season? season, int year)> GetCurrentSeasonAndYear(
        CancellationToken cancellationToken)
    {
        var today = DateTime.Now;
        var currentMonth = today.Month;
        var currentDay = today.Day;

        var allSeasons = await _unitOfWork.Repository<Season>()
            .ListAsync(_ => true);

        foreach (var season in allSeasons)
        {
            if (IsDateInSeasonRange(currentMonth, currentDay, season.StartDate, season.EndDate))
            {
                var startParts = season.StartDate.Split('/');
                int startMonth = int.Parse(startParts[0]);

                int year = today.Year;
                if (currentMonth < startMonth && startMonth > 6)
                {
                    year--;
                }

                return (season, year);
            }
        }

        return (null, today.Year);
    }

    private bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr)
    {
        try
        {
            var startParts = startDateStr.Split('/');
            var endParts = endDateStr.Split('/');

            int startMonth = int.Parse(startParts[0]);
            int startDay = int.Parse(startParts[1]);
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);

            int currentDate = month * 100 + day;
            int seasonStart = startMonth * 100 + startDay;
            int seasonEnd = endMonth * 100 + endDay;

            if (seasonStart > seasonEnd)
            {
                return currentDate >= seasonStart || currentDate <= seasonEnd;
            }
            else
            {
                return currentDate >= seasonStart && currentDate <= seasonEnd;
            }
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Calculate area in hectares from a polygon with SRID 4326 (WGS84)
    /// </summary>
    private decimal CalculateAreaInHectares(Polygon polygon)
    {
        // For SRID 4326 (WGS84 lat/lon), approximate conversion
        // This uses a simple approximation - area in square degrees * conversion factor
        var areaInSquareDegrees = polygon.Area;
        
        // Approximate meters per degree at equator (111,319.9 meters per degree)
        var metersPerDegree = 111319.9;
        var areaInSquareMeters = areaInSquareDegrees * metersPerDegree * metersPerDegree;
        
        // Convert to hectares (1 hectare = 10,000 square meters)
        var hectares = (decimal)(areaInSquareMeters / 10000.0);
        
        return Math.Round(hectares, 2);
    }
    
    /// <summary>
    /// Get existing YearSeason or create a new one if it doesn't exist
    /// </summary>
    private async Task<YearSeason?> GetOrCreateYearSeasonAsync(
        Season season, 
        int year, 
        Guid clusterId, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to find existing YearSeason
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .FindAsync(ys => ys.SeasonId == season.Id && 
                                ys.Year == year &&
                                ys.ClusterId == clusterId);
            
            if (yearSeason != null)
            {
                _logger.LogInformation(
                    "Đã tìm thấy Năm-Vụ mùa hiện có: {YearSeasonId}, Cho phép nông dân chọn: {AllowSelection}",
                    yearSeason.Id,
                    yearSeason.AllowFarmerSelection);
                return yearSeason;
            }
            
            // Auto-create YearSeason if not found
            _logger.LogInformation(
                "Không tìm thấy Năm-Vụ mùa cho Vụ mùa: {SeasonName}, Năm: {Year}, Cụm: {ClusterId}. Đang tự động tạo...",
                season.SeasonName,
                year,
                clusterId);
            
            // Parse season dates (DD/MM format)
            var seasonStart = ParseSeasonDate(season.StartDate, year);
            var seasonEnd = ParseSeasonDate(season.EndDate, year);
            
            // Handle year wraparound (e.g., Winter-Spring: 11/01 to 04/30)
            if (seasonEnd < seasonStart)
            {
                seasonEnd = seasonEnd.AddYears(1);
            }
            
            // Create new YearSeason
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
                Notes = "Được tạo tự động khi nhập thửa đất. Vui lòng xem xét và cấu hình cài đặt.",
                AllowedPlantingFlexibilityDays = 7,
                PlanningWindowStart = DateTime.UtcNow,
                PlanningWindowEnd = seasonStart.AddDays(-3)
            };
            
            await _unitOfWork.Repository<YearSeason>().AddAsync(yearSeason);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
                _logger.LogInformation(
                    "Đã tự động tạo Năm-Vụ mùa: {YearSeasonId} cho {SeasonName} {Year} trong Cụm {ClusterId}",
                yearSeason.Id,
                season.SeasonName,
                year,
                clusterId);
            
            return yearSeason;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Lỗi khi lấy hoặc tạo Năm-Vụ mùa cho Vụ mùa: {SeasonName}, Năm: {Year}",
                season.SeasonName,
                year);
            return null;
        }
    }
    
    /// <summary>
    /// Parse season date from DD/MM format to actual DateTime
    /// </summary>
    private static DateTime ParseSeasonDate(string ddmmString, int year)
    {
        var parts = ddmmString.Split('/');
        var day = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);
        return new DateTime(year, month, day);
    }
}
