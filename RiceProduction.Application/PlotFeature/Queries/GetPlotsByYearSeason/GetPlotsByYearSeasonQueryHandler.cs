using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsByYearSeason;

public class GetPlotsByYearSeasonQueryHandler 
    : IRequestHandler<GetPlotsByYearSeasonQuery, PagedResult<IEnumerable<PlotWithSeasonInfoDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlotsByYearSeasonQueryHandler> _logger;

    public GetPlotsByYearSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPlotsByYearSeasonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<IEnumerable<PlotWithSeasonInfoDto>>> Handle(
        GetPlotsByYearSeasonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Fetching plots for YearSeason {YearSeasonId} - Page: {PageNumber}, PageSize: {PageSize}",
                request.YearSeasonId, request.PageNumber, request.PageSize);

            // Get YearSeason with related data
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Include(ys => ys.Season)
                .Include(ys => ys.RiceVariety)
                .Include(ys => ys.Cluster)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return PagedResult<IEnumerable<PlotWithSeasonInfoDto>>.Failure(
                    error: "YearSeason not found",
                    message: $"YearSeason with ID {request.YearSeasonId} not found");
            }

            // Build base predicate for plots
            Expression<Func<Plot, bool>> predicate = p => p.Status == PlotStatus.Active;

            // Filter by cluster (through farmer)
            var targetClusterId = yearSeason.ClusterId;

            // If ClusterManagerId is provided, validate it belongs to this cluster
            if (request.ClusterManagerId.HasValue)
            {
                var clusterManager = await _unitOfWork.ClusterManagerRepository
                    .GetClusterManagerByIdAsync(request.ClusterManagerId.Value, cancellationToken);

                if (clusterManager == null || clusterManager.ClusterId != targetClusterId)
                {
                    return PagedResult<IEnumerable<PlotWithSeasonInfoDto>>.Success(
                        data: Enumerable.Empty<PlotWithSeasonInfoDto>(),
                        currentPage: request.PageNumber,
                        pageSize: request.PageSize,
                        totalCount: 0,
                        message: "Cluster manager not found or doesn't manage this cluster");
                }
            }

            // Filter by cluster
            predicate = predicate.And(p => p.Farmer != null && p.Farmer.ClusterId == targetClusterId);

            // Search term filter
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var search = request.SearchTerm.Trim();
                predicate = predicate.And(p =>
                    (p.SoThua.HasValue && p.SoThua.Value.ToString().Contains(search)) ||
                    (p.SoTo.HasValue && p.SoTo.Value.ToString().Contains(search)) ||
                    (p.Farmer != null && !string.IsNullOrEmpty(p.Farmer.FullName) && 
                     p.Farmer.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            // Get plots with includes
            var plotsQuery = _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Where(predicate)
                .Include(p => p.Farmer)
                .Include(p => p.GroupPlots)
                    .ThenInclude(gp => gp.Group)
                .Include(p => p.PlotCultivations.Where(pc => pc.SeasonId == yearSeason.SeasonId))
                    .ThenInclude(pc => pc.RiceVariety)
                .AsQueryable();

            // Filter by group if specified
            if (request.GroupId.HasValue)
            {
                plotsQuery = plotsQuery.Where(p => 
                    p.GroupPlots.Any(gp => 
                        gp.GroupId == request.GroupId.Value && 
                        gp.Group.YearSeasonId == request.YearSeasonId));
            }
            else if (request.IsInGroup.HasValue)
            {
                if (request.IsInGroup.Value)
                {
                    plotsQuery = plotsQuery.Where(p => 
                        p.GroupPlots.Any(gp => gp.Group.YearSeasonId == request.YearSeasonId));
                }
                else
                {
                    plotsQuery = plotsQuery.Where(p => 
                        !p.GroupPlots.Any(gp => gp.Group.YearSeasonId == request.YearSeasonId));
                }
            }

            // Get total count before pagination
            var totalCount = await plotsQuery.CountAsync(cancellationToken);

            // Apply pagination
            var plots = await plotsQuery
                .OrderBy(p => p.SoThua ?? 0)
                .ThenBy(p => p.SoTo ?? 0)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Map to DTOs
            var geoJsonWriter = new GeoJsonWriter();
            var dtos = new List<PlotWithSeasonInfoDto>();

            foreach (var plot in plots)
            {
                var plotCultivation = plot.PlotCultivations.FirstOrDefault();
                var groupPlot = plot.GroupPlots.FirstOrDefault(gp => 
                    gp.Group.YearSeasonId == request.YearSeasonId);

                var dto = new PlotWithSeasonInfoDto
                {
                    // Plot info
                    PlotId = plot.Id,
                    FarmerId = plot.FarmerId,
                    FarmerName = plot.Farmer?.FullName ?? "Unknown",
                    FarmerPhoneNumber = plot.Farmer?.PhoneNumber,
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    Area = plot.Area,
                    SoilType = plot.SoilType,
                    PlotStatus = plot.Status,
                    BoundaryGeoJson = plot.Boundary != null ? geoJsonWriter.Write(plot.Boundary) : null,
                    CoordinateGeoJson = plot.Coordinate != null ? geoJsonWriter.Write(plot.Coordinate) : null,
                    GroupId = groupPlot?.GroupId,
                    GroupName = groupPlot?.Group?.GroupName,

                    // YearSeason info
                    YearSeasonId = yearSeason.Id,
                    SeasonName = yearSeason.Season?.SeasonName ?? "Unknown",
                    Year = yearSeason.Year,
                    AllowFarmerSelection = yearSeason.RiceVarietyId == null,
                    YearSeasonRiceVarietyId = yearSeason.RiceVarietyId,
                    YearSeasonRiceVarietyName = yearSeason.RiceVariety?.VarietyName,

                    // PlotCultivation info
                    PlotCultivationId = plotCultivation?.Id,
                    SelectedRiceVarietyId = plotCultivation?.RiceVarietyId,
                    SelectedRiceVarietyName = plotCultivation?.RiceVariety?.VarietyName,
                    SelectedPlantingDate = plotCultivation?.PlantingDate,
                    CultivationStatus = plotCultivation?.Status,
                    IsFarmerConfirmed = plotCultivation?.IsFarmerConfirmed,
                    FarmerSelectionDate = plotCultivation?.FarmerSelectionDate,
                    FarmerSelectionNotes = plotCultivation?.FarmerSelectionNotes,

                    // Computed fields
                    HasMadeSelection = plotCultivation != null,
                    SelectionStatusMessage = BuildSelectionStatusMessage(yearSeason, plotCultivation)
                };

                dtos.Add(dto);
            }

            // Filter by selection status if specified
            if (request.HasMadeSelection.HasValue)
            {
                dtos = dtos.Where(d => d.HasMadeSelection == request.HasMadeSelection.Value).ToList();
                totalCount = dtos.Count;
            }

            _logger.LogInformation(
                "Retrieved {Count} plots for YearSeason {YearSeasonId}",
                dtos.Count, request.YearSeasonId);

            return PagedResult<IEnumerable<PlotWithSeasonInfoDto>>.Success(
                data: dtos,
                currentPage: request.PageNumber,
                pageSize: request.PageSize,
                totalCount: totalCount,
                message: "Plots retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error fetching plots for YearSeason {YearSeasonId}", 
                request.YearSeasonId);
            return PagedResult<IEnumerable<PlotWithSeasonInfoDto>>.Failure(
                error: "An error occurred while fetching plots",
                message: $"Failed to retrieve plots: {ex.Message}");
        }
    }

    private static string BuildSelectionStatusMessage(YearSeason yearSeason, PlotCultivation? plotCultivation)
    {
        if (plotCultivation == null)
        {
            if (yearSeason.RiceVarietyId == null)
            {
                return "Farmer can select rice variety and planting date";
            }
            else
            {
                return $"Expert has selected {yearSeason.RiceVariety?.VarietyName ?? "a rice variety"} for this season. Farmer needs to confirm planting date.";
            }
        }

        if (plotCultivation.IsFarmerConfirmed)
        {
            return $"Selection confirmed: {plotCultivation.RiceVariety?.VarietyName} on {plotCultivation.PlantingDate:yyyy-MM-dd}";
        }

        return $"Selection pending confirmation: {plotCultivation.RiceVariety?.VarietyName} on {plotCultivation.PlantingDate:yyyy-MM-dd}";
    }
}

// Helper extension for combining predicates
public static class PredicateExtensions
{
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(left, parameter),
            Expression.Invoke(right, parameter));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

