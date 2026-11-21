using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.ExportPlotData;

public class ExportPlotDataQueryHandler : IRequestHandler<ExportPlotDataQuery, Result<FileExportResult>>
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

    public async Task<Result<FileExportResult>> Handle(
        ExportPlotDataQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = _unitOfWork.Repository<Plot>().GetQueryable()
                .AsNoTracking()
                .Include(p => p.Farmer)
                .Include(p => p.GroupPlots)
                    .ThenInclude(gp => gp.Group)
                .Include(p => p.PlotCultivations)
                    .ThenInclude(pc => pc.RiceVariety)
                .AsSplitQuery();

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
                return Result<FileExportResult>.Failure("No plots found to export");
            }

            var allSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
            var currentSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, DateTime.Now)?.Season;

            var exportRows = plots.Select(plot => MapToExportRow(plot, currentSeason)).ToList();

            var filterDesc = request switch
            {
                { FarmerId: not null } => "Farmer",
                { GroupId: not null } => "Group",
                { ClusterManagerId: not null } => "Cluster",
                _ => "All"
            };

            var now = DateTime.Now;
            var fileName = $"Plot_Export_{filterDesc}_{now:yyyyMMdd_HHmmss}.xlsx";

            var result = await _genericExcel.DownloadGenericExcelFile(
                exportRows,
                now.ToString("yyyy-MM-dd"),
                fileName);

            if (result == null)
            {
                _logger.LogError("Failed to generate Excel file");
                return Result<FileExportResult>.Failure("Failed to generate Excel file");
            }

            _logger.LogInformation(
                "Exported {PlotCount} plots to Excel. Filters: ClusterManager={ClusterManagerId}, Farmer={FarmerId}, Group={GroupId}",
                exportRows.Count,
                request.ClusterManagerId,
                request.FarmerId,
                request.GroupId);

            return Result<FileExportResult>.Success(
                result,
                $"Successfully exported {exportRows.Count} plots");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting plot data");
            return Result<FileExportResult>.Failure($"Failed to export plot data: {ex.Message}");
        }
    }

    private static PlotExportRow MapToExportRow(Plot plot, Season? currentSeason)
    {
        var latestGroupPlot = plot.GroupPlots
            .OrderByDescending(gp => gp.CreatedAt)
            .FirstOrDefault();

        var currentCultivation = currentSeason != null
            ? plot.PlotCultivations
                .Where(pc => pc.SeasonId == currentSeason.Id &&
                           (pc.Status == CultivationStatus.Planned ||
                            pc.Status == CultivationStatus.InProgress))
                .OrderByDescending(pc => pc.CreatedAt)
                .FirstOrDefault()
            : null;

        return new PlotExportRow
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
    }
}

