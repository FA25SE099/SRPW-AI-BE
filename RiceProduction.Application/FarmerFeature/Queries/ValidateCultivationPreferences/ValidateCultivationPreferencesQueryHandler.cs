using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Queries.ValidateCultivationPreferences;

public class ValidateCultivationPreferencesQueryHandler 
    : IRequestHandler<ValidateCultivationPreferencesQuery, Result<CultivationValidationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly ILogger<ValidateCultivationPreferencesQueryHandler> _logger;

    public ValidateCultivationPreferencesQueryHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        ILogger<ValidateCultivationPreferencesQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<CultivationValidationDto>> Handle(
        ValidateCultivationPreferencesQuery request, 
        CancellationToken cancellationToken)
    {
        var validation = new CultivationValidationDto();

        try
        {
            // 1. Get YearSeason
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Include(ys => ys.Season)
                .Include(ys => ys.RiceVariety)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "YEARSEASON_NOT_FOUND",
                    Message = "The specified season instance does not exist",
                    Severity = "Error"
                });
                return Result<CultivationValidationDto>.Success(validation);
            }

            // 2. Check if farmer selection is enabled
            if (!yearSeason.AllowFarmerSelection)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "SELECTION_NOT_ALLOWED",
                    Message = "Farmer selection is not enabled for this season",
                    Severity = "Error"
                });
                return Result<CultivationValidationDto>.Success(validation);
            }

            // 3. Check selection window
            var now = DateTime.UtcNow;
            if (yearSeason.FarmerSelectionWindowStart.HasValue && 
                now < yearSeason.FarmerSelectionWindowStart.Value)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "SELECTION_NOT_STARTED",
                    Message = $"Selection window opens on {yearSeason.FarmerSelectionWindowStart.Value:yyyy-MM-dd}",
                    Severity = "Error"
                });
            }

            if (yearSeason.FarmerSelectionWindowEnd.HasValue && 
                now > yearSeason.FarmerSelectionWindowEnd.Value)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "SELECTION_CLOSED",
                    Message = $"Selection window closed on {yearSeason.FarmerSelectionWindowEnd.Value:yyyy-MM-dd}",
                    Severity = "Error"
                });
            }

            // 4. Get Plot and verify ownership
            var plot = await _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.Id == request.PlotId, cancellationToken);

            if (plot == null)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "PLOT_NOT_FOUND",
                    Message = "The specified plot does not exist",
                    Severity = "Error"
                });
                return Result<CultivationValidationDto>.Success(validation);
            }

            if (_currentUser.Id.HasValue && plot.FarmerId != _currentUser.Id.Value)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "PLOT_NOT_OWNED",
                    Message = "You do not own this plot",
                    Severity = "Error"
                });
                return Result<CultivationValidationDto>.Success(validation);
            }

            // 5. Get RiceVariety and check if suitable for season
            var riceVarietySeason = await _unitOfWork.Repository<RiceVarietySeason>()
                .GetQueryable()
                .Include(rvs => rvs.RiceVariety)
                .FirstOrDefaultAsync(rvs => 
                    rvs.RiceVarietyId == request.RiceVarietyId && 
                    rvs.SeasonId == yearSeason.SeasonId, 
                    cancellationToken);

            if (riceVarietySeason == null)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "VARIETY_NOT_SUITABLE",
                    Message = "This rice variety is not suitable for the selected season",
                    Severity = "Error"
                });
                return Result<CultivationValidationDto>.Success(validation);
            }

            // 6. Check planting date within YearSeason dates
            if (request.PreferredPlantingDate < yearSeason.StartDate)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "PLANTING_DATE_TOO_EARLY",
                    Message = $"Planting date must be after {yearSeason.StartDate:yyyy-MM-dd}",
                    Severity = "Error"
                });
            }

            if (request.PreferredPlantingDate > yearSeason.EndDate)
            {
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "PLANTING_DATE_TOO_LATE",
                    Message = $"Planting date must be before {yearSeason.EndDate:yyyy-MM-dd}",
                    Severity = "Error"
                });
            }

            // 8. Check for existing cultivation
            var existingCultivation = await _unitOfWork.Repository<PlotCultivation>()
                .FindAsync(pc => 
                    pc.PlotId == request.PlotId && 
                    pc.YearSeasonId == request.YearSeasonId
                    );

            if (existingCultivation != null && existingCultivation.FarmerSelectionDate != null)
            {
                validation.Warnings.Add(new ValidationIssue
                {
                    Code = "EXISTING_SELECTION",
                    Message = "You have already made a selection for this plot. This will update your previous choice.",
                    Severity = "Info"
                });
            }

            validation.GrowthDurationDays = riceVarietySeason.RiceVariety.BaseGrowthDurationDays;
            validation.EstimatedHarvestDate = request.PreferredPlantingDate
                .AddDays(validation.GrowthDurationDays.Value);

            if (plot.Area != null && riceVarietySeason.ExpectedYieldPerHectare.HasValue)
            {
                validation.ExpectedYield = plot.Area * riceVarietySeason.ExpectedYieldPerHectare.Value;
            }

            // 10. Add recommendations
            if (!riceVarietySeason.IsRecommended)
            {
                validation.Recommendations.Add(new ValidationRecommendation
                {
                    Title = "Not Recommended",
                    Description = "This variety is not recommended for this season. Consider choosing a recommended variety for better results.",
                    Type = "Warning"
                });
            }

            // Set overall validity
            validation.IsValid = validation.Errors.Count == 0;

            _logger.LogInformation(
                "Validated cultivation preferences for plot {PlotId}: Valid={IsValid}, Errors={ErrorCount}", 
                request.PlotId, validation.IsValid, validation.Errors.Count);

            return Result<CultivationValidationDto>.Success(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error validating cultivation preferences for plot {PlotId}", 
                request.PlotId);
            return Result<CultivationValidationDto>.Failure(
                "An error occurred while validating your selection");
        }
    }
}

