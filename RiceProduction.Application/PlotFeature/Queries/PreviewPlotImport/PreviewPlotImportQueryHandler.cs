using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Domain.Entities;

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

            var wktReader = new WKTReader(new GeometryFactory(new PrecisionModel(), 4326));

            var allSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
            var today = DateTime.Now;
            var resolvedSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, today);
            var currentSeason = resolvedSeason?.Season;
            var currentYear = resolvedSeason?.Year ?? today.Year;
            var seasonLookup = allSeasons.ToDictionary(s => s.SeasonName, s => s, StringComparer.OrdinalIgnoreCase);

            var uniqueFarmCodes = plotImportRows
                .Where(p => !string.IsNullOrWhiteSpace(p.FarmCode))
                .Select(p => p.FarmCode)
                .Distinct()
                .ToList();

            var farmers = await farmerRepo.ListAsync(f => uniqueFarmCodes.Contains(f.FarmCode));
            var farmerLookup = farmers.ToDictionary(f => f.FarmCode, f => f);

            var uniqueVarietyNames = plotImportRows
                .Where(p => !string.IsNullOrWhiteSpace(p.RiceVarietyName))
                .Select(p => p.RiceVarietyName)
                .Distinct()
                .ToList();

            var riceVarieties = await riceVarietyRepo.ListAsync(
                rv => uniqueVarietyNames.Contains(rv.VarietyName));
            var varietyLookup = riceVarieties.ToDictionary(rv => rv.VarietyName, rv => rv);

            for (int i = 0; i < plotImportRows.Count; i++)
            {
                var row = plotImportRows[i];

                // Skip empty template rows
                if (!row.SoThua.HasValue && !row.SoTo.HasValue && !row.Area.HasValue)
                {
                    preview.SkippedRowsCount++;
                    continue;
                }

                // Excel data rows start at row 2 (row 1 is header)
                var previewRow = CreatePreviewRow(row, rowNumber: i + 2);

                ValidateBasicFields(row, previewRow, farmerLookup);
                ValidateCultivationFields(row, previewRow, varietyLookup, seasonLookup, currentSeason, currentYear);
                ValidatePolygon(row, previewRow, wktReader);

                // Check for duplicates only when the row has no validation errors
                if (!previewRow.Errors.Any() && farmerLookup.TryGetValue(row.FarmCode, out var farmer))
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

                CategorizePreviewRow(preview, previewRow);
            }

            preview.Summary = BuildSummary(preview);

            return Result<PlotImportPreviewDto>.Success(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xem trước nhập thửa đất");
            return Result<PlotImportPreviewDto>.Failure(
                $"Xem trước thất bại: {ex.Message}");
        }
    }

    private static PlotImportPreviewRow CreatePreviewRow(PlotImportRow row, int rowNumber) =>
        new()
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
            Year = row.Year,
            // Include polygon WKT data so frontend doesn't need to read Excel again
            BoundaryWKT = row.BoundaryWKT,
            CoordinateWKT = row.CoordinateWKT
        };

    private static void ValidateBasicFields(
        PlotImportRow row,
        PlotImportPreviewRow previewRow,
        Dictionary<string, Farmer> farmerLookup)
    {
        if (string.IsNullOrWhiteSpace(row.FarmCode))
        {
            previewRow.Errors.Add("Mã nông hộ (FarmCode) là bắt buộc");
        }
        else if (!farmerLookup.ContainsKey(row.FarmCode))
        {
            previewRow.Errors.Add($"Không tìm thấy nông hộ '{row.FarmCode}'. Vui lòng nhập danh sách nông hộ trước.");
        }

        if (!row.SoThua.HasValue || row.SoThua.Value <= 0)
        {
            previewRow.Errors.Add("Số thửa là bắt buộc và phải > 0");
        }

        if (!row.SoTo.HasValue || row.SoTo.Value <= 0)
        {
            previewRow.Errors.Add("Số tờ là bắt buộc và phải > 0");
        }

        if (!row.Area.HasValue || row.Area.Value <= 0)
        {
            previewRow.Errors.Add("Diện tích là bắt buộc và phải > 0");
        }
    }

    private static void ValidateCultivationFields(
        PlotImportRow row,
        PlotImportPreviewRow previewRow,
        Dictionary<string, RiceVariety> varietyLookup,
        Dictionary<string, Season> seasonLookup,
        Season? currentSeason,
        int currentYear)
    {
        if (string.IsNullOrWhiteSpace(row.RiceVarietyName))
        {
            previewRow.WillCreatePlotCultivation = "Không (Chưa chỉ định giống lúa)";
            return;
        }

        if (!varietyLookup.ContainsKey(row.RiceVarietyName))
        {
            previewRow.Errors.Add($"Không tìm thấy giống lúa '{row.RiceVarietyName}'. Vui lòng kiểm tra trang 'Rice_Varieties'.");
        }
        else
        {
            var seasonToUse = !string.IsNullOrWhiteSpace(row.SeasonName) &&
                              seasonLookup.TryGetValue(row.SeasonName, out var specifiedSeason)
                ? specifiedSeason
                : currentSeason;

            if (seasonToUse != null)
            {
                var cultivationYear = row.Year ?? currentYear;
                previewRow.SeasonYear = $"{seasonToUse.SeasonName} {cultivationYear}";
                previewRow.WillCreatePlotCultivation = "Có";
            }
            else if (currentSeason == null && string.IsNullOrWhiteSpace(row.SeasonName))
            {
                previewRow.Errors.Add("Không thể tạo canh tác - không tìm thấy vụ mùa hiện tại và không có vụ mùa được chỉ định");
            }
        }

        if (!string.IsNullOrWhiteSpace(row.SeasonName) && !seasonLookup.ContainsKey(row.SeasonName))
        {
            var availableSeasons = string.Join(", ", seasonLookup.Keys);
            previewRow.Errors.Add($"Không tìm thấy vụ mùa '{row.SeasonName}'. Các vụ mùa có sẵn: {availableSeasons}");
        }

        if (row.Year.HasValue)
        {
            var minYear = DateTime.Now.Year - 5;
            var maxYear = DateTime.Now.Year + 5;

            if (row.Year.Value < minYear || row.Year.Value > maxYear)
            {
                previewRow.Errors.Add($"Năm phải nằm trong khoảng {minYear} và {maxYear}");
            }
        }
    }

    private static void ValidatePolygon(
        PlotImportRow row,
        PlotImportPreviewRow previewRow,
        WKTReader wktReader)
    {
        if (string.IsNullOrWhiteSpace(row.BoundaryWKT))
        {
            previewRow.PlotStatus = "Đang chờ vẽ đa giác (giám sát viên sẽ vẽ ranh giới)";
            previewRow.Warnings.Add("Chưa cung cấp đa giác. Thửa đất sẽ yêu cầu giám sát viên vẽ ranh giới.");
            return;
        }

        try
        {
            var boundary = wktReader.Read(row.BoundaryWKT) as Polygon;
            if (boundary == null)
            {
                previewRow.Errors.Add("Định dạng đa giác không hợp lệ. Yêu cầu định dạng WKT POLYGON.");
                return;
            }

            if (!boundary.IsValid)
            {
                previewRow.Errors.Add("Đa giác không hợp lệ về mặt hình học.");
                return;
            }

            boundary.SRID = 4326;

            // Validate polygon area is within 10% tolerance of registered area
            if (row.Area.HasValue)
            {
                var drawnAreaHa = PlotGeometryHelper.CalculateAreaInHectares(boundary, decimalPlaces: 2);
                var registeredArea = row.Area.Value;
                var differencePercent = Math.Abs((drawnAreaHa - registeredArea) / registeredArea * 100);

                if (differencePercent > 10)
                {
                    previewRow.Errors.Add(
                        $"Diện tích đa giác ({Math.Round(drawnAreaHa, 2)} ha) chênh lệch {Math.Round(differencePercent, 2)}% so với diện tích đăng ký ({Math.Round(registeredArea, 2)} ha). Mức chênh lệch tối đa cho phép là 10%.");
                }
            }

            if (!string.IsNullOrWhiteSpace(row.CoordinateWKT))
            {
                var coordinate = wktReader.Read(row.CoordinateWKT) as Point;
                if (coordinate == null)
                {
                    previewRow.Warnings.Add("Định dạng tọa độ không hợp lệ. Sẽ sử dụng tâm đa giác thay thế.");
                }
                else if (!boundary.Contains(coordinate))
                {
                    previewRow.Warnings.Add("Tọa độ nằm ngoài ranh giới đa giác. Sẽ sử dụng tâm đa giác thay thế.");
                }
            }

            previewRow.HasPolygon = true;
            previewRow.PlotStatus = "Hoạt động";
        }
        catch (Exception ex)
        {
            previewRow.Errors.Add($"Lỗi khi phân tích đa giác: {ex.Message}");
        }
    }

    private static void CategorizePreviewRow(PlotImportPreviewDto preview, PlotImportPreviewRow previewRow)
    {
        if (previewRow.Errors.Any())
        {
            previewRow.Status = "Không hợp lệ";
            preview.InvalidRowsCount++;
            preview.InvalidRows.Add(previewRow);
            return;
        }

        previewRow.Status = previewRow.Warnings.Any() ? "Cảnh báo" : "Hợp lệ";
        preview.ValidRowsCount++;
        preview.ValidRows.Add(previewRow);
    }

    private static Dictionary<string, object> BuildSummary(PlotImportPreviewDto preview) =>
        new()
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
}
