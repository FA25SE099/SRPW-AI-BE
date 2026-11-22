using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonFarmerSelections;

public class GetYearSeasonFarmerSelectionsQueryHandler 
    : IRequestHandler<GetYearSeasonFarmerSelectionsQuery, Result<YearSeasonFarmerSelectionsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetYearSeasonFarmerSelectionsQueryHandler> _logger;

    public GetYearSeasonFarmerSelectionsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetYearSeasonFarmerSelectionsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<YearSeasonFarmerSelectionsDto>> Handle(
        GetYearSeasonFarmerSelectionsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var yearSeason = await _context.YearSeasons
                .AsNoTracking()
                .Include(ys => ys.Season)
                .Include(ys => ys.Cluster)
                .Include(ys => ys.RiceVariety)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<YearSeasonFarmerSelectionsDto>.Failure(
                    $"YearSeason with ID {request.YearSeasonId} not found");
            }

            var now = DateTime.UtcNow;
            var isSelectionWindowOpen = yearSeason.AllowFarmerSelection &&
                (!yearSeason.FarmerSelectionWindowStart.HasValue || now >= yearSeason.FarmerSelectionWindowStart.Value) &&
                (!yearSeason.FarmerSelectionWindowEnd.HasValue || now <= yearSeason.FarmerSelectionWindowEnd.Value);

            var farmersList = await _context.Farmers
                .AsNoTracking()
                .Where(f => f.ClusterId == yearSeason.ClusterId)
                .ToListAsync(cancellationToken);
            var farmerIds = farmersList.Select(f => f.Id).ToList();
            var totalFarmers = farmersList.Count;

            var plotsList = await _context.Plots
                .AsNoTracking()
                .Where(p => farmerIds.Contains(p.FarmerId))
                .ToListAsync(cancellationToken);
            var plotById = plotsList.ToDictionary(p => p.Id);
            var plotIds = plotById.Keys.ToList();

            var cultivations = await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == yearSeason.SeasonId)
                .ToListAsync(cancellationToken);

            var farmersWithSelection = cultivations
                .Select(pc => plotById[pc.PlotId].FarmerId)
                .Distinct()
                .ToList();

            var varietyIds = cultivations.Select(pc => pc.RiceVarietyId).Distinct().ToList();
            var varietyDict = await _context.RiceVarieties
                .AsNoTracking()
                .Where(rv => varietyIds.Contains(rv.Id))
                .ToDictionaryAsync(rv => rv.Id, cancellationToken);

            var previousCultivations = await GetPreviousSeasonCultivationsAsync(
                yearSeason, plotIds, cancellationToken);

            var varietySelections = BuildVarietySelections(
                cultivations, previousCultivations, varietyDict, yearSeason.RiceVarietyId, totalFarmers);

            var pendingFarmerIds = farmerIds.Except(farmersWithSelection).ToList();
            var pendingFarmers = BuildPendingFarmers(
                pendingFarmerIds, farmersList, plotsList, previousCultivations, varietyDict);

            var result = new YearSeasonFarmerSelectionsDto
            {
                YearSeasonId = yearSeason.Id,
                SeasonName = yearSeason.Season?.SeasonName ?? "Unknown",
                Year = yearSeason.Year,
                ClusterName = yearSeason.Cluster?.ClusterName ?? "Unknown",
                ClusterRiceVarietyId = yearSeason.RiceVarietyId,
                ClusterRiceVarietyName = yearSeason.RiceVariety?.VarietyName,
                AllowFarmerSelection = yearSeason.AllowFarmerSelection,
                SelectionWindowStart = yearSeason.FarmerSelectionWindowStart,
                SelectionWindowEnd = yearSeason.FarmerSelectionWindowEnd,
                IsSelectionWindowOpen = isSelectionWindowOpen,
                SelectionStatus = new FarmerSelectionStatus
                {
                    TotalFarmers = totalFarmers,
                    FarmersWithSelection = farmersWithSelection.Count,
                    FarmersPending = totalFarmers - farmersWithSelection.Count,
                    SelectionCompletionRate = CalculatePercentage(farmersWithSelection.Count, totalFarmers),
                    VarietySelections = varietySelections,
                    PendingFarmers = pendingFarmers
                }
            };

            return Result<YearSeasonFarmerSelectionsDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting farmer selections for YearSeason {YearSeasonId}",
                request.YearSeasonId);
            return Result<YearSeasonFarmerSelectionsDto>.Failure(
                $"Error getting farmer selections: {ex.Message}");
        }
    }

    private static List<VarietySelectionSummary> BuildVarietySelections(
        List<PlotCultivation> cultivations,
        List<PlotCultivation> previousCultivations,
        Dictionary<Guid, RiceVariety> varietyDict,
        Guid? recommendedVarietyId,
        int totalFarmers)
    {
        var previousCountByVariety = previousCultivations
            .GroupBy(pc => pc.RiceVarietyId)
            .ToDictionary(g => g.Key, g => g.Count());

        return cultivations
            .GroupBy(pc => pc.RiceVarietyId)
            .Select(g =>
            {
                var varietyId = g.Key;
                var currentCount = g.Count();
                var previousCount = previousCountByVariety.GetValueOrDefault(varietyId);

                return new VarietySelectionSummary
                {
                    VarietyId = varietyId,
                    VarietyName = varietyDict.GetValueOrDefault(varietyId)?.VarietyName ?? "Unknown",
                    SelectedByCount = currentCount,
                    PreviousSeasonCount = previousCount,
                    IsRecommended = varietyId == recommendedVarietyId,
                    NewSelections = 0,
                    SwitchedIn = Math.Max(0, currentCount - previousCount),
                    SwitchedOut = Math.Max(0, previousCount - currentCount),
                    PercentageOfTotal = CalculatePercentage(currentCount, totalFarmers)
                };
            })
            .OrderByDescending(v => v.SelectedByCount)
            .ToList();
    }

    private static List<PendingFarmerInfo> BuildPendingFarmers(
        List<Guid> pendingFarmerIds,
        List<Farmer> farmersList,
        List<Plot> plotsList,
        List<PlotCultivation> previousCultivations,
        Dictionary<Guid, RiceVariety> varietyDict)
    {
        var farmerDict = farmersList.ToDictionary(f => f.Id);
        var plotsByFarmerId = plotsList.ToLookup(p => p.FarmerId);

        return pendingFarmerIds
            .Select(fid =>
            {
                var farmer = farmerDict.GetValueOrDefault(fid);
                var farmerPlots = plotsByFarmerId[fid].ToList();
                var farmerPlotIds = farmerPlots.Select(p => p.Id).ToHashSet();
                var previousVariety = previousCultivations
                    .Where(pc => farmerPlotIds.Contains(pc.PlotId))
                    .Select(pc => varietyDict.GetValueOrDefault(pc.RiceVarietyId)?.VarietyName)
                    .FirstOrDefault();

                return new PendingFarmerInfo
                {
                    FarmerId = fid,
                    FarmerName = farmer?.FullName ?? "Unknown",
                    PhoneNumber = farmer?.PhoneNumber,
                    PreviousVariety = previousVariety,
                    PlotCount = farmerPlots.Count,
                    TotalArea = farmerPlots.Sum(p => p.Area)
                };
            })
            .OrderBy(pf => pf.FarmerName)
            .ToList();
    }

    private static decimal CalculatePercentage(int count, int total) =>
        total > 0 ? (decimal)count / total * 100 : 0;

    private async Task<List<PlotCultivation>> GetPreviousSeasonCultivationsAsync(
        YearSeason currentYearSeason,
        List<Guid> plotIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var previousYear = currentYearSeason.Year - 1;

            var hasPreviousYearSeason = await _context.YearSeasons
                .AsNoTracking()
                .AnyAsync(ys =>
                    ys.ClusterId == currentYearSeason.ClusterId &&
                    ys.SeasonId == currentYearSeason.SeasonId &&
                    ys.Year == previousYear,
                    cancellationToken);

            if (!hasPreviousYearSeason)
            {
                return new List<PlotCultivation>();
            }

            return await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotIds.Contains(pc.PlotId) &&
                             pc.SeasonId == currentYearSeason.SeasonId)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not retrieve previous season cultivations for comparison");
            return new List<PlotCultivation>();
        }
    }
}
