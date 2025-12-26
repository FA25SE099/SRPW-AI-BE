using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonFarmerSelections;

public class GetYearSeasonFarmerSelectionsQueryHandler 
    : IRequestHandler<GetYearSeasonFarmerSelectionsQuery, Result<YearSeasonFarmerSelectionsDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetYearSeasonFarmerSelectionsQueryHandler> _logger;

    public GetYearSeasonFarmerSelectionsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetYearSeasonFarmerSelectionsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<YearSeasonFarmerSelectionsDto>> Handle(
        GetYearSeasonFarmerSelectionsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load YearSeason
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
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

            // Get all farmers in the cluster
            var farmers = await _unitOfWork.FarmerRepository
                .ListAsync(f => f.ClusterId == yearSeason.ClusterId);
            var farmersList = farmers.ToList();
            var farmerIds = farmersList.Select(f => f.Id).ToList();
            var totalFarmers = farmersList.Count;

            // Get all plots for these farmers
            var plots = await _unitOfWork.Repository<Plot>()
                .ListAsync(p => farmerIds.Contains(p.FarmerId));
            var plotsList = plots.ToList();
            var plotIds = plotsList.Select(p => p.Id).ToList();

            // Get plot cultivations for this season
            var cultivations = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == yearSeason.SeasonId)
                .ToListAsync(cancellationToken);

            // Get farmers with selections
            var farmersWithSelection = cultivations
                .Select(pc => plotsList.First(p => p.Id == pc.PlotId).FarmerId)
                .Distinct()
                .ToList();

            var farmersPending = totalFarmers - farmersWithSelection.Count;
            var completionRate = totalFarmers > 0 
                ? (decimal)farmersWithSelection.Count / totalFarmers * 100 
                : 0;

            // Get variety selections
            var varietyIds = cultivations.Select(pc => pc.RiceVarietyId).Distinct().ToList();
            var varieties = await _unitOfWork.Repository<RiceVariety>()
                .ListAsync(rv => varietyIds.Contains(rv.Id));
            var varietyDict = varieties.ToDictionary(rv => rv.Id);

            // Get previous season data for comparison
            var previousCultivations = await GetPreviousSeasonCultivationsAsync(
                yearSeason, plotIds, cancellationToken);

            var varietySelections = cultivations
                .GroupBy(pc => pc.RiceVarietyId)
                .Select(g =>
                {
                    var varietyId = g.Key;
                    var currentCount = g.Count();
                    var previousCount = previousCultivations.Count(pc => pc.RiceVarietyId == varietyId);

                    return new VarietySelectionSummary
                    {
                        VarietyId = varietyId,
                        VarietyName = varietyDict.GetValueOrDefault(varietyId)?.VarietyName ?? "Unknown",
                        SelectedByCount = currentCount,
                        PreviousSeasonCount = previousCount,
                        IsRecommended = varietyId == yearSeason.RiceVarietyId, // Cluster-selected variety
                        NewSelections = 0, // Can be enhanced with farmer tracking
                        SwitchedIn = Math.Max(0, currentCount - previousCount),
                        SwitchedOut = Math.Max(0, previousCount - currentCount),
                        PercentageOfTotal = totalFarmers > 0 
                            ? (decimal)currentCount / totalFarmers * 100 
                            : 0
                    };
                })
                .OrderByDescending(v => v.SelectedByCount)
                .ToList();

            // Build pending farmers list
            var farmerDict = farmersList.ToDictionary(f => f.Id);
            var pendingFarmerIds = farmerIds.Except(farmersWithSelection).ToList();
            var pendingFarmers = pendingFarmerIds.Select(fid =>
            {
                var farmer = farmerDict.GetValueOrDefault(fid);
                var farmerPlots = plotsList.Where(p => p.FarmerId == fid).ToList();
                var previousVariety = previousCultivations
                    .Where(pc => farmerPlots.Any(p => p.Id == pc.PlotId))
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
                    FarmersPending = farmersPending,
                    SelectionCompletionRate = completionRate,
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

    private async Task<List<PlotCultivation>> GetPreviousSeasonCultivationsAsync(
        YearSeason currentYearSeason,
        List<Guid> plotIds,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the same season type from previous year
            var previousYear = currentYearSeason.Year - 1;

            var previousYearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .FirstOrDefaultAsync(ys => 
                    ys.ClusterId == currentYearSeason.ClusterId &&
                    ys.SeasonId == currentYearSeason.SeasonId &&
                    ys.Year == previousYear,
                    cancellationToken);

            if (previousYearSeason == null)
            {
                return new List<PlotCultivation>();
            }

            var previousCultivations = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .Where(pc => plotIds.Contains(pc.PlotId) && 
                            pc.SeasonId == previousYearSeason.SeasonId)
                .ToListAsync(cancellationToken);

            return previousCultivations;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Could not retrieve previous season cultivations for comparison");
            return new List<PlotCultivation>();
        }
    }
}

