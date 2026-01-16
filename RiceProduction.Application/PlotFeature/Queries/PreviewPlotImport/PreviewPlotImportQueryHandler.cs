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
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.PreviewPlotImport;

public class PreviewPlotImportQueryHandler 
    : IRequestHandler<PreviewPlotImportQuery, Result<PlotImportPreviewDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericExcel _genericExcel;
    private readonly ILogger<PreviewPlotImportQueryHandler> _logger;

    public PreviewPlotImportQueryHandler(
        IUnitOfWork unitOfWork, 
        IGenericExcel genericExcel, 
        ILogger<PreviewPlotImportQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _genericExcel = genericExcel;
        _logger = logger;
    }

    public async Task<Result<PlotImportPreviewDto>> Handle(
        PreviewPlotImportQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var preview = new PlotImportPreviewDto();

            // Read Excel file
            var plotImportRows = await _genericExcel.ExcelToListT<PlotImportRow>(request.ExcelFile);
            if (plotImportRows == null || !plotImportRows.Any())
            {
                preview.GeneralErrors.Add("Tệp Excel đã tải lên trống hoặc không hợp lệ.");
                return Result<PlotImportPreviewDto>.Success(preview);
            }

            preview.TotalRows = plotImportRows.Count;

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

            // Validate and build preview rows
            for (int i = 0; i < plotImportRows.Count; i++)
            {
                var row = plotImportRows[i];
                var rowNumber = i + 2; // Excel rows start at 2 (1 is header)

                var previewRow = new PlotImportPreviewRow
                {
                    RowNumber = rowNumber,
                    FarmCode = row.FarmCode ?? string.Empty,
                    FarmerName = row.FarmerName ?? string.Empty,
                    PhoneNumber = row.PhoneNumber ?? string.Empty,
                    SoThua = row.SoThua,
                    SoTo = row.SoTo,
                    Area = row.Area,
                    SoilType = row.SoilType,
                    RiceVarietyName = row.RiceVarietyName,
                    PlantingDate = row.PlantingDate,
                    SeasonName = row.SeasonName,
                    Year = row.Year
                };

                // Skip rows with no plot data (empty rows from template)
                if (!row.SoThua.HasValue && !row.SoTo.HasValue && !row.Area.HasValue)
                {
                    preview.SkippedRowsCount++;
                    continue;
                }

                var hasErrors = false;

                // Validate FarmCode
                if (string.IsNullOrWhiteSpace(row.FarmCode))
                {
                    previewRow.Errors.Add("Mã nông hộ (FarmCode) là bắt buộc");
                    hasErrors = true;
                }
                else if (!farmerLookup.ContainsKey(row.FarmCode))
                {
                    previewRow.Errors.Add($"Không tìm thấy nông hộ '{row.FarmCode}'. Vui lòng nhập danh sách nông hộ trước.");
                    hasErrors = true;
                }

                // Validate required numeric fields
                if (!row.SoThua.HasValue || row.SoThua.Value <= 0)
                {
                    previewRow.Errors.Add("Số thửa là bắt buộc và phải > 0");
                    hasErrors = true;
                }

                if (!row.SoTo.HasValue || row.SoTo.Value <= 0)
                {
                    previewRow.Errors.Add("Số tờ là bắt buộc và phải > 0");
                    hasErrors = true;
                }

                if (!row.Area.HasValue || row.Area.Value <= 0)
                {
                    previewRow.Errors.Add("Diện tích là bắt buộc và phải > 0");
                    hasErrors = true;
                }

                // Validate rice variety if provided
                if (!string.IsNullOrWhiteSpace(row.RiceVarietyName))
                {
                    if (!varietyLookup.ContainsKey(row.RiceVarietyName))
                    {
                        previewRow.Errors.Add($"Không tìm thấy giống lúa '{row.RiceVarietyName}'. Vui lòng kiểm tra trang 'Rice_Varieties'.");
                        hasErrors = true;
                    }
                    else
                    {
                        // Determine season and year for cultivation
                        Season? seasonToUse = null;
                        if (!string.IsNullOrWhiteSpace(row.SeasonName) && seasonLookup.TryGetValue(row.SeasonName, out var specifiedSeason))
                        {
                            seasonToUse = specifiedSeason;
                        }
                        else
                        {
                            seasonToUse = currentSeason;
                        }

                        if (seasonToUse != null)
                        {
                            var cultivationYear = row.Year ?? currentYear;
                            previewRow.SeasonYear = $"{seasonToUse.SeasonName} {cultivationYear}";
                            previewRow.WillCreatePlotCultivation = "Có";
                        }
                        else if (currentSeason == null && string.IsNullOrWhiteSpace(row.SeasonName))
                        {
                            previewRow.Errors.Add("Không thể tạo canh tác - không tìm thấy vụ mùa hiện tại và không có vụ mùa được chỉ định");
                            hasErrors = true;
                        }
                    }

                    // Validate season name if provided
                    if (!string.IsNullOrWhiteSpace(row.SeasonName))
                    {
                        if (!seasonLookup.ContainsKey(row.SeasonName))
                        {
                            var availableSeasons = string.Join(", ", seasonLookup.Keys);
                            previewRow.Errors.Add($"Không tìm thấy vụ mùa '{row.SeasonName}'. Các vụ mùa có sẵn: {availableSeasons}");
                            hasErrors = true;
                        }
                    }
                    
                    // Validate year if provided
                    if (row.Year.HasValue)
                    {
                        var minYear = DateTime.Now.Year - 5;
                        var maxYear = DateTime.Now.Year + 5;
                        
                        if (row.Year.Value < minYear || row.Year.Value > maxYear)
                        {
                            previewRow.Errors.Add($"Năm phải nằm trong khoảng {minYear} và {maxYear}");
                            hasErrors = true;
                        }
                    }
                }
                else
                {
                    previewRow.WillCreatePlotCultivation = "Không (Chưa chỉ định giống lúa)";
                }

                // Validate polygon if provided
                if (!string.IsNullOrWhiteSpace(row.BoundaryWKT))
                {
                    try
                    {
                        var boundary = wktReader.Read(row.BoundaryWKT) as Polygon;
                        
                        if (boundary == null)
                        {
                            previewRow.Errors.Add("Định dạng đa giác không hợp lệ. Yêu cầu định dạng WKT POLYGON.");
                            hasErrors = true;
                        }
                        else if (!boundary.IsValid)
                        {
                            previewRow.Errors.Add("Đa giác không hợp lệ về mặt hình học.");
                            hasErrors = true;
                        }
                        else
                        {
                            boundary.SRID = 4326;

                            // Validate area matches (10% tolerance)
                            if (row.Area.HasValue)
                            {
                                var drawnAreaHa = CalculateAreaInHectares(boundary);
                                var registeredArea = row.Area.Value;
                                var differencePercent = Math.Abs((drawnAreaHa - registeredArea) / registeredArea * 100);

                                if (differencePercent > 10)
                                {
                                    previewRow.Errors.Add(
                                        $"Diện tích đa giác ({Math.Round(drawnAreaHa, 2)} ha) chênh lệch {Math.Round(differencePercent, 2)}% so với diện tích đăng ký ({Math.Round(registeredArea, 2)} ha). Mức chênh lệch tối đa cho phép là 10%.");
                                    hasErrors = true;
                                }
                            }

                            // Parse coordinate or use centroid
                            Point coordinate;
                            if (!string.IsNullOrWhiteSpace(row.CoordinateWKT))
                            {
                                coordinate = wktReader.Read(row.CoordinateWKT) as Point;
                                if (coordinate == null)
                                {
                                    previewRow.Warnings.Add("Định dạng tọa độ không hợp lệ. Sẽ sử dụng tâm đa giác thay thế.");
                                    coordinate = boundary.Centroid;
                                }
                                else if (!boundary.Contains(coordinate))
                                {
                                    previewRow.Warnings.Add("Tọa độ nằm ngoài ranh giới đa giác. Sẽ sử dụng tâm đa giác thay thế.");
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

                            // Store validated polygon
                            validatedPolygons[i] = (boundary, coordinate);
                            previewRow.HasPolygon = true;
                            previewRow.PlotStatus = "Hoạt động";
                        }
                    }
                    catch (Exception ex)
                    {
                        previewRow.Errors.Add($"Lỗi khi phân tích đa giác: {ex.Message}");
                        hasErrors = true;
                    }
                }
                else
                {
                    previewRow.PlotStatus = "Đang chờ vẽ đa giác (giám sát viên sẽ vẽ ranh giới)";
                    previewRow.Warnings.Add("Chưa cung cấp đa giác. Thửa đất sẽ yêu cầu giám sát viên vẽ ranh giới.");
                }

                // Check for duplicates (only if row is otherwise valid)
                if (!hasErrors && farmerLookup.TryGetValue(row.FarmCode, out var farmer))
                {
                    var existingPlot = await plotRepo.FindAsync(p => 
                        p.SoThua == row.SoThua && 
                        p.SoTo == row.SoTo && 
                        p.FarmerId == farmer.Id);
                        
                    if (existingPlot != null)
                    {
                        previewRow.IsDuplicate = true;
                        previewRow.Status = "Trùng lặp";
                        previewRow.Warnings.Add($"Thửa đất với Số thửa={row.SoThua} và Số tờ={row.SoTo} đã tồn tại cho nông hộ này. Sẽ được bỏ qua khi nhập.");
                        preview.DuplicateRowsCount++;
                        preview.InvalidRowsCount++;
                        preview.InvalidRows.Add(previewRow);
                        continue;
                    }
                }

                // Categorize row
                if (hasErrors)
                {
                    previewRow.Status = "Không hợp lệ";
                    preview.InvalidRowsCount++;
                    preview.InvalidRows.Add(previewRow);
                }
                else if (previewRow.Warnings.Any())
                {
                    previewRow.Status = "Cảnh báo";
                    preview.ValidRowsCount++;
                    preview.ValidRows.Add(previewRow);
                }
                else
                {
                    previewRow.Status = "Hợp lệ";
                    preview.ValidRowsCount++;
                    preview.ValidRows.Add(previewRow);
                }
            }

            // Build summary
            preview.Summary = new Dictionary<string, object>
            {
                { "TotalRows", preview.TotalRows },
                { "ValidRows", preview.ValidRowsCount },
                { "InvalidRows", preview.InvalidRowsCount },
                { "SkippedRows", preview.SkippedRowsCount },
                { "DuplicateRows", preview.DuplicateRowsCount },
                { "RowsWithPolygons", preview.ValidRows.Count(r => r.HasPolygon) },
                { "RowsWithoutPolygons", preview.ValidRows.Count(r => !r.HasPolygon) },
                { "RowsCreatingCultivation", preview.ValidRows.Count(r => r.WillCreatePlotCultivation == "Yes") }
            };

            return Result<PlotImportPreviewDto>.Success(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xem trước nhập thửa đất");
            return Result<PlotImportPreviewDto>.Failure(
                $"Xem trước thất bại: {ex.Message}");
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
}
