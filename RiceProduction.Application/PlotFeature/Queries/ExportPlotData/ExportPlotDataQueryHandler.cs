using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.ExportPlotData;

public class ExportPlotDataQueryHandler : IRequestHandler<ExportPlotDataQuery, Result<IActionResult>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericExcel _genericExcel;
    private readonly ILogger<ExportPlotDataQueryHandler> _logger;

    public ExportPlotDataQueryHandler(
        IUnitOfWork unitOfWork,
        IGenericExcel genericExcel,
        ILogger<ExportPlotDataQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _genericExcel = genericExcel;
        _logger = logger;
    }

    public async Task<Result<IActionResult>> Handle(
        ExportPlotDataQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build query
            var query = _unitOfWork.Repository<Plot>().GetQueryable()
                .Include(p => p.Farmer)
                .Include(p => p.GroupPlots)
                    .ThenInclude(gp => gp.Group)
                .Include(p => p.PlotCultivations)
                    .ThenInclude(pc => pc.RiceVariety)
                .AsQueryable();

            // Apply filters
            if (request.ClusterManagerId.HasValue)
            {
                var clusterManager = await _unitOfWork.ClusterManagerRepository
                    .GetClusterManagerByIdAsync(request.ClusterManagerId.Value, cancellationToken);
                
                if (clusterManager?.ClusterId != null)
                {
                    query = query.Where(p => p.Farmer.ClusterId == clusterManager.ClusterId);
                }
            }

            if (request.FarmerId.HasValue)
            {
                query = query.Where(p => p.FarmerId == request.FarmerId.Value);
            }

            if (request.GroupId.HasValue)
            {
                query = query.Where(p => p.GroupPlots.Any(gp => gp.GroupId == request.GroupId.Value));
            }

            // Filter by polygon status
            if (request.OnlyWithPolygons)
            {
                query = query.Where(p => p.Status == PlotStatus.Active && p.Boundary != null);
            }
            else if (request.OnlyWithoutPolygons)
            {
                query = query.Where(p => p.Status == PlotStatus.PendingPolygon);
            }

            var plots = await query
                .OrderBy(p => p.Farmer.FarmCode)
                .ThenBy(p => p.SoThua)
                .ThenBy(p => p.SoTo)
                .ToListAsync(cancellationToken);

            if (!plots.Any())
            {
                _logger.LogWarning("No plots found for export with the given filters");
                return Result<IActionResult>.Failure("No plots found to export");
            }

            // Get current season for cultivation data
            var (currentSeason, currentYear) = await GetCurrentSeasonAndYear(cancellationToken);

            // Map to export rows
            var exportRows = new List<PlotExportRow>();
            
            foreach (var plot in plots)
            {
                var latestGroupPlot = plot.GroupPlots
                    .OrderByDescending(gp => gp.CreatedAt)
                    .FirstOrDefault();

                // Get current cultivation (if any)
                PlotCultivation? currentCultivation = null;
                if (currentSeason != null)
                {
                    currentCultivation = plot.PlotCultivations
                        .Where(pc => pc.SeasonId == currentSeason.Id && 
                                   (pc.Status == CultivationStatus.Planned || 
                                    pc.Status == CultivationStatus.InProgress))
                        .OrderByDescending(pc => pc.CreatedAt)
                        .FirstOrDefault();
                }

                var exportRow = new PlotExportRow
                {
                    FarmCode = plot.Farmer?.FarmCode ?? "",
                    FarmerName = plot.Farmer?.FullName ?? "",
                    PhoneNumber = plot.Farmer?.PhoneNumber ?? "",
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    Area = plot.Area,
                    SoilType = plot.SoilType,
                    Status = plot.Status.ToString(),
                    BoundaryWKT = plot.Boundary?.AsText(),
                    CoordinateWKT = plot.Coordinate?.AsText(),
                    CurrentRiceVariety = currentCultivation?.RiceVariety?.VarietyName,
                    GroupName = latestGroupPlot?.Group?.GroupName
                };

                exportRows.Add(exportRow);
            }

            // Generate filename
            var filterDesc = request.FarmerId.HasValue ? "Farmer" :
                           request.GroupId.HasValue ? "Group" :
                           request.ClusterManagerId.HasValue ? "Cluster" :
                           "All";
            
            var fileName = $"Plot_Export_{filterDesc}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            
            var result = await _genericExcel.DownloadGenericExcelFile(
                exportRows,
                DateTime.Now.ToString("yyyy-MM-dd"),
                fileName);

            if (result == null)
            {
                _logger.LogError("Failed to generate Excel file");
                return Result<IActionResult>.Failure("Failed to generate Excel file");
            }

            _logger.LogInformation(
                "Exported {PlotCount} plots to Excel. Filters: ClusterManager={ClusterManagerId}, Farmer={FarmerId}, Group={GroupId}",
                exportRows.Count,
                request.ClusterManagerId,
                request.FarmerId,
                request.GroupId);

            return Result<IActionResult>.Success(
                result,
                $"Successfully exported {exportRows.Count} plots");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting plot data");
            return Result<IActionResult>.Failure($"Failed to export plot data: {ex.Message}");
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

