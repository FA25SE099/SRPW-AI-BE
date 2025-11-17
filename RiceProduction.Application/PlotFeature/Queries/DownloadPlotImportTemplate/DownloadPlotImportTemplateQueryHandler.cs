using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
            // Get cluster from cluster manager
            Guid? clusterId = null;
            if (request.ClusterManagerId.HasValue)
            {
                var clusterManager = await _unitOfWork.ClusterManagerRepository
                    .GetClusterManagerByIdAsync(request.ClusterManagerId.Value, cancellationToken);
                clusterId = clusterManager?.ClusterId;
            }

            // Get farmers in cluster
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

            // Get available rice varieties with their category info
            var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
                .ListAsync(rv => rv.IsActive);
            var riceVarietiesList = riceVarieties.OrderBy(rv => rv.VarietyName).ToList();

            // Get current season to show season-specific info
            var (currentSeason, currentYear) = await GetCurrentSeasonAndYear(cancellationToken);

            // Create template rows - ONE ROW PER PLOT based on farmer's NumberOfPlots
            var templateRows = new List<PlotImportRow>();
            
            foreach (var farmer in farmersList)
            {
                // Use the farmer's NumberOfPlots, default to 1 if not set
                int plotCount = farmer.NumberOfPlots ?? 1;
                
                // Create a row for each plot
                for (int plotNum = 1; plotNum <= plotCount; plotNum++)
                {
                    var row = new PlotImportRow
                    {
                        FarmCode = farmer.FarmCode ?? "",
                        FarmerName = farmer.FullName ?? "",
                        PhoneNumber = farmer.PhoneNumber ?? "",
                        PlotNumber = plotNum,
                        SoThua = null,
                        SoTo = null,
                        Area = null,
                        SoilType = null,
                        RiceVarietyName = null
                    };

                    templateRows.Add(row);
                }
            }

            // Get season information for varieties
            var seasonInfo = currentSeason != null 
                ? $"{currentSeason.SeasonName} ({currentSeason.StartDate} - {currentSeason.EndDate})"
                : "All Seasons";

            // Create rice variety reference data
            var varietyReferences = riceVarietiesList.Select(rv => new RiceVarietyReference
            {
                VarietyName = rv.VarietyName ?? "",
                SeasonType = seasonInfo,
                GrowthDuration = rv.BaseGrowthDurationDays.HasValue 
                    ? $"{rv.BaseGrowthDurationDays} days" 
                    : "Not specified",
                Description = rv.Description ?? rv.Characteristics ?? ""
            }).ToList();

            // Add instruction row if no rice varieties found
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

            // Create multi-sheet Excel using IGenericExcel
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

    private async Task<(Season? season, int year)> GetCurrentSeasonAndYear(CancellationToken cancellationToken)
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
}

