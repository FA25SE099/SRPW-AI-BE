using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmerCultivationSelections;

public class GetFarmerCultivationSelectionsQueryHandler 
    : IRequestHandler<GetFarmerCultivationSelectionsQuery, Result<FarmerCultivationSelectionsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetFarmerCultivationSelectionsQueryHandler> _logger;

    public GetFarmerCultivationSelectionsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetFarmerCultivationSelectionsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<FarmerCultivationSelectionsDto>> Handle(
        GetFarmerCultivationSelectionsQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Get YearSeason details
            var yearSeason = await _context.YearSeasons
                .AsNoTracking()
                .Include(ys => ys.Season)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<FarmerCultivationSelectionsDto>.Failure("Year Season not found");
            }

            // 2. Get the farmer to check their cluster
            var farmer = await _context.Farmers
                .AsNoTracking()
                .Where(f => f.Id == request.FarmerId)
                .Select(f => new { f.Id, f.ClusterId })
                .FirstOrDefaultAsync(cancellationToken);

            if (farmer == null)
            {
                return Result<FarmerCultivationSelectionsDto>.Failure("Farmer not found");
            }

            // Verify farmer is in the same cluster as the year season
            if (farmer.ClusterId != yearSeason.ClusterId)
            {
                _logger.LogWarning(
                    "Farmer {FarmerId} in cluster {FarmerClusterId} attempted to access YearSeason {YearSeasonId} in cluster {YearSeasonClusterId}",
                    request.FarmerId, farmer.ClusterId, request.YearSeasonId, yearSeason.ClusterId);
                return Result<FarmerCultivationSelectionsDto>.Failure(
                    "This season is not available in your cluster");
            }

            // 3. Get all plots belonging to the farmer (no group requirement)
            var farmerPlots = await _context.Plots
                .AsNoTracking()
                .Where(p => p.FarmerId == request.FarmerId && p.Status == PlotStatus.Active)
                .Select(p => new { p.Id, p.SoThua, p.SoTo, p.Area })
                .ToListAsync(cancellationToken);

            if (farmerPlots.Count == 0)
            {
                return Result<FarmerCultivationSelectionsDto>.Failure(
                    "No plots found for this farmer");
            }

            // 4. Get existing cultivations for these plots
            var plotIds = farmerPlots.Select(p => p.Id).ToList();
            var cultivations = await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotIds.Contains(pc.PlotId) && 
                            pc.YearSeasonId == request.YearSeasonId)
                .Select(pc => new
                {
                    pc.PlotId,
                    pc.RiceVarietyId,
                    VarietyName = pc.RiceVariety != null ? pc.RiceVariety.VarietyName : null,
                    BaseGrowthDurationDays = pc.RiceVariety != null ? (int?)pc.RiceVariety.BaseGrowthDurationDays : null,
                    pc.PlantingDate,
                    pc.FarmerSelectionDate
                })
                .ToListAsync(cancellationToken);

            // Expected yields for the selected varieties (one query instead of one per plot)
            var varietyIds = cultivations.Select(c => c.RiceVarietyId).Distinct().ToList();
            var yields = await _context.RiceVarietySeasons
                .AsNoTracking()
                .Where(rvs => varietyIds.Contains(rvs.RiceVarietyId) && rvs.SeasonId == yearSeason.SeasonId)
                .Select(rvs => new { rvs.RiceVarietyId, rvs.ExpectedYieldPerHectare })
                .ToListAsync(cancellationToken);

            // 5. Create selection DTOs
            var selections = new List<PlotCultivationSelectionDto>();

            foreach (var plot in farmerPlots)
            {
                var cultivation = cultivations.FirstOrDefault(c => c.PlotId == plot.Id);
                
                var selection = new PlotCultivationSelectionDto
                {
                    PlotId = plot.Id,
                    PlotName = "Thửa " + plot.SoThua + "/" + plot.SoTo,
                    PlotArea = plot.Area,
                    IsConfirmed = cultivation?.FarmerSelectionDate != null,
                    RiceVarietyId = cultivation?.RiceVarietyId,
                    RiceVarietyName = cultivation?.VarietyName,
                    PlantingDate = cultivation?.PlantingDate,
                    SelectionDate = cultivation?.FarmerSelectionDate
                };

                if (cultivation?.PlantingDate != null && cultivation.BaseGrowthDurationDays != null)
                {
                    selection.EstimatedHarvestDate = cultivation.PlantingDate
                        .AddDays((double)cultivation.BaseGrowthDurationDays.Value);
                }

                if (cultivation != null)
                {
                    var expectedYield = yields
                        .FirstOrDefault(rvs => rvs.RiceVarietyId == cultivation.RiceVarietyId)
                        ?.ExpectedYieldPerHectare;

                    if (expectedYield != null)
                    {
                        selection.ExpectedYield = plot.Area * expectedYield.Value;
                    }
                }

                selections.Add(selection);
            }

            // 6. Calculate summary statistics
            var confirmedPlots = selections.Count(s => s.IsConfirmed);
            var pendingPlots = selections.Count - confirmedPlots;

            // 7. Calculate days until deadline
            var daysUntilDeadline = 0;
            if (yearSeason.FarmerSelectionWindowEnd.HasValue)
            {
                var timeSpan = yearSeason.FarmerSelectionWindowEnd.Value - DateTime.UtcNow;
                daysUntilDeadline = (int)Math.Max(0, Math.Ceiling(timeSpan.TotalDays));
            }

            // 8. Create result DTO
            var result = new FarmerCultivationSelectionsDto
            {
                YearSeasonId = yearSeason.Id,
                SeasonName = yearSeason.Season.SeasonName,
                Year = yearSeason.Year,
                SelectionDeadline = yearSeason.FarmerSelectionWindowEnd,
                DaysUntilDeadline = daysUntilDeadline,
                TotalPlots = farmerPlots.Count,
                ConfirmedPlots = confirmedPlots,
                PendingPlots = pendingPlots,
                Selections = selections.OrderByDescending(s => s.IsConfirmed)
                                      .ThenBy(s => s.PlotName)
                                      .ToList()
            };

            _logger.LogInformation(
                "Retrieved {TotalPlots} plots for farmer {FarmerId}, {ConfirmedPlots} confirmed", 
                result.TotalPlots, request.FarmerId, result.ConfirmedPlots);

            return Result<FarmerCultivationSelectionsDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error retrieving cultivation selections for farmer {FarmerId}", 
                request.FarmerId);
            return Result<FarmerCultivationSelectionsDto>.Failure(
                "An error occurred while retrieving your selections");
        }
    }
}
