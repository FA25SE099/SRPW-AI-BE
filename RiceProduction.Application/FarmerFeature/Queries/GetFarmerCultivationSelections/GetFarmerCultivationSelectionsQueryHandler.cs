using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmerCultivationSelections;

public class GetFarmerCultivationSelectionsQueryHandler 
    : IRequestHandler<GetFarmerCultivationSelectionsQuery, Result<FarmerCultivationSelectionsDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetFarmerCultivationSelectionsQueryHandler> _logger;

    public GetFarmerCultivationSelectionsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetFarmerCultivationSelectionsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<FarmerCultivationSelectionsDto>> Handle(
        GetFarmerCultivationSelectionsQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Get YearSeason details
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Include(ys => ys.Season)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<FarmerCultivationSelectionsDto>.Failure("Year Season not found");
            }

            // 2. Get all plots belonging to the farmer in the cluster
            var farmerPlots = await _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Include(p => p.GroupPlots)
                    .ThenInclude(gp => gp.Group)
                .Where(p => p.FarmerId == request.FarmerId && 
                           p.GroupPlots.Any(gp => gp.Group.ClusterId == yearSeason.ClusterId))
                .ToListAsync(cancellationToken);

            if (!farmerPlots.Any())
            {
                return Result<FarmerCultivationSelectionsDto>.Failure(
                    "No plots found for this farmer in the cluster");
            }

            // 3. Get existing cultivations for these plots
            var plotIds = farmerPlots.Select(p => p.Id).ToList();
            var cultivations = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .Include(pc => pc.Plot)
                .Include(pc => pc.RiceVariety)
                .Where(pc => plotIds.Contains(pc.PlotId) && 
                            pc.YearSeasonId == request.YearSeasonId)
                .ToListAsync(cancellationToken);

            // 4. Create selection DTOs
            var selections = new List<PlotCultivationSelectionDto>();

            foreach (var plot in farmerPlots)
            {
                var cultivation = cultivations.FirstOrDefault(c => c.PlotId == plot.Id);
                
                var selection = new PlotCultivationSelectionDto
                {
                    PlotId = plot.Id,
                    PlotName = "",
                    PlotArea = plot.Area,
                    IsConfirmed = cultivation?.FarmerSelectionDate != null,
                    RiceVarietyId = cultivation?.RiceVarietyId,
                    RiceVarietyName = cultivation?.RiceVariety?.VarietyName,
                    PlantingDate = cultivation?.PlantingDate,
                    SelectionDate = cultivation?.FarmerSelectionDate
                };

                // Calculate estimated harvest date if we have planting date and variety
                if (cultivation?.PlantingDate != null && cultivation.RiceVariety != null)
                {
                    selection.EstimatedHarvestDate = cultivation.PlantingDate
                        .AddDays((double)cultivation.RiceVariety.BaseGrowthDurationDays);
                }

                if ( cultivation?.RiceVariety != null)
                {
                    var riceVarietySeason = await _unitOfWork.Repository<RiceVarietySeason>()
                        .FindAsync(rvs => 
                            rvs.RiceVarietyId == cultivation.RiceVarietyId && 
                            rvs.SeasonId == yearSeason.SeasonId
                            );

                    if (riceVarietySeason?.ExpectedYieldPerHectare != null)
                    {
                        selection.ExpectedYield = plot.Area * riceVarietySeason.ExpectedYieldPerHectare.Value;
                    }
                }

                selections.Add(selection);
            }

            // 5. Calculate summary statistics
            var confirmedPlots = selections.Count(s => s.IsConfirmed);
            var pendingPlots = selections.Count - confirmedPlots;

            // 6. Calculate days until deadline
            var daysUntilDeadline = 0;
            if (yearSeason.FarmerSelectionWindowEnd.HasValue)
            {
                var timeSpan = yearSeason.FarmerSelectionWindowEnd.Value - DateTime.UtcNow;
                daysUntilDeadline = (int)Math.Max(0, Math.Ceiling(timeSpan.TotalDays));
            }

            // 7. Create result DTO
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

