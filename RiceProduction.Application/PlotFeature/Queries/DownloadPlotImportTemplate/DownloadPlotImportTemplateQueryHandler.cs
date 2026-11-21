using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Queries.DownloadPlotImportTemplate;

public class DownloadPlotImportTemplateQueryHandler
    : IRequestHandler<DownloadPlotImportTemplateQuery, Result<IActionResult>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericExcel _genericExcel;
    private readonly ILogger<DownloadPlotImportTemplateQueryHandler> _logger;

    public DownloadPlotImportTemplateQueryHandler(
        IUnitOfWork unitOfWork,
        IGenericExcel genericExcel,
        ILogger<DownloadPlotImportTemplateQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _genericExcel = genericExcel;
        _logger = logger;
    }

    public async Task<Result<IActionResult>> Handle(
        DownloadPlotImportTemplateQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            Guid? clusterId = null;
            if (request.ClusterManagerId.HasValue)
            {
                var clusterManager = await _unitOfWork.ClusterManagerRepository
                    .GetClusterManagerByIdAsync(request.ClusterManagerId.Value, cancellationToken);
                clusterId = clusterManager?.ClusterId;
            }

            var farmers = clusterId.HasValue
                ? await _unitOfWork.FarmerRepository.ListAsync(f => f.ClusterId == clusterId.Value)
                : await _unitOfWork.FarmerRepository.ListAsync(f => true);

            var farmersList = farmers
                .Where(f => !string.IsNullOrEmpty(f.FarmCode))
                .OrderBy(f => f.FarmCode)
                .ToList();

            if (!farmersList.Any())
            {
                _logger.LogWarning("No farmers found for template generation");
                return Result<IActionResult>.Failure(
                    "No farmers found. Please import farmers first using the farmer import template.");
            }

            var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
                .ListAsync(rv => rv.IsActive);
            var riceVarietiesList = riceVarieties.OrderBy(rv => rv.VarietyName).ToList();

            var allSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
            var currentSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, DateTime.Now)?.Season;

            var templateRows = BuildTemplateRows(farmersList);
            var varietyReferences = BuildVarietyReferences(riceVarietiesList, currentSeason);

            var sheets = new Dictionary<string, object>
            {
                { "Plot_Import", templateRows },
                { "Rice_Varieties", varietyReferences }
            };

            var fileName = $"Plot_Import_Template_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var result = await _genericExcel.DownloadMultiSheetExcelFile(sheets, fileName);

            if (result == null)
            {
                _logger.LogError("Failed to generate multi-sheet Excel file");
                return Result<IActionResult>.Failure("Failed to generate Excel template");
            }

            _logger.LogInformation(
                "Generated plot import template: {FarmerCount} farmers, {PlotRows} plot rows, {VarietyCount} varieties",
                farmersList.Count,
                templateRows.Count,
                riceVarietiesList.Count);

            return Result<IActionResult>.Success(
                result,
                $"Template generated successfully: {farmersList.Count} farmers with {templateRows.Count} plot rows");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating plot import template");
            return Result<IActionResult>.Failure($"Failed to generate template: {ex.Message}");
        }
    }

    private static List<PlotImportRow> BuildTemplateRows(List<Farmer> farmers)
    {
        var templateRows = new List<PlotImportRow>();

        foreach (var farmer in farmers)
        {
            var plotCount = farmer.NumberOfPlots ?? 1;

            for (int plotNum = 1; plotNum <= plotCount; plotNum++)
            {
                templateRows.Add(new PlotImportRow
                {
                    FarmCode = farmer.FarmCode ?? "",
                    FarmerName = farmer.FullName ?? "",
                    PhoneNumber = farmer.PhoneNumber ?? "",
                    PlotNumber = plotNum,
                    SoThua = null,
                    SoTo = null,
                    Area = null,
                    SoilType = null,
                    RiceVarietyName = null,
                    BoundaryWKT = null,
                    CoordinateWKT = null
                });
            }
        }

        return templateRows;
    }

    private static List<RiceVarietyReference> BuildVarietyReferences(
        List<RiceVariety> riceVarieties,
        Season? currentSeason)
    {
        var seasonInfo = currentSeason != null
            ? $"{currentSeason.SeasonName} ({currentSeason.StartDate} - {currentSeason.EndDate})"
            : "All Seasons";

        var varietyReferences = riceVarieties.Select(rv => new RiceVarietyReference
        {
            VarietyName = rv.VarietyName ?? "",
            SeasonType = seasonInfo,
            GrowthDuration = rv.BaseGrowthDurationDays.HasValue
                ? $"{rv.BaseGrowthDurationDays} days"
                : "Not specified",
            Description = rv.Description ?? rv.Characteristics ?? ""
        }).ToList();

        if (!varietyReferences.Any())
        {
            varietyReferences.Add(new RiceVarietyReference
            {
                VarietyName = "No rice varieties found",
                SeasonType = "Please add rice varieties first",
                GrowthDuration = "",
                Description = "Contact administrator to add rice variety data"
            });
        }

        return varietyReferences;
    }
}

