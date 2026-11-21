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
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Include(ys => ys.Season)
                .Include(ys => ys.RiceVariety)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return FailureResult(validation, "YEARSEASON_NOT_FOUND", "The specified season instance does not exist");
            }

            if (!yearSeason.AllowFarmerSelection)
            {
                return FailureResult(validation, "SELECTION_NOT_ALLOWED", "Farmer selection is not enabled for this season");
            }

            var now = DateTime.UtcNow;
            if (yearSeason.FarmerSelectionWindowStart.HasValue && now < yearSeason.FarmerSelectionWindowStart.Value)
            {
                AddError(
                    validation,
                    "SELECTION_NOT_STARTED",
                    $"Selection window opens on {yearSeason.FarmerSelectionWindowStart.Value:yyyy-MM-dd}");
            }

            if (yearSeason.FarmerSelectionWindowEnd.HasValue && now > yearSeason.FarmerSelectionWindowEnd.Value)
            {
                AddError(
                    validation,
                    "SELECTION_CLOSED",
                    $"Selection window closed on {yearSeason.FarmerSelectionWindowEnd.Value:yyyy-MM-dd}");
            }

            var plot = await _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.Id == request.PlotId, cancellationToken);

            if (plot == null)
            {
                return FailureResult(validation, "PLOT_NOT_FOUND", "The specified plot does not exist");
            }

            if (_currentUser.Id.HasValue && plot.FarmerId != _currentUser.Id.Value)
            {
                return FailureResult(validation, "PLOT_NOT_OWNED", "You do not own this plot");
            }

            var riceVarietySeason = await _unitOfWork.Repository<RiceVarietySeason>()
                .GetQueryable()
                .Include(rvs => rvs.RiceVariety)
                .FirstOrDefaultAsync(
                    rvs => rvs.RiceVarietyId == request.RiceVarietyId && rvs.SeasonId == yearSeason.SeasonId,
                    cancellationToken);

            if (riceVarietySeason == null)
            {
                return FailureResult(validation, "VARIETY_NOT_SUITABLE", "This rice variety is not suitable for the selected season");
            }

            if (request.PreferredPlantingDate < yearSeason.StartDate)
            {
                AddError(
                    validation,
                    "PLANTING_DATE_TOO_EARLY",
                    $"Planting date must be after {yearSeason.StartDate:yyyy-MM-dd}");
            }

            if (request.PreferredPlantingDate > yearSeason.EndDate)
            {
                AddError(
                    validation,
                    "PLANTING_DATE_TOO_LATE",
                    $"Planting date must be before {yearSeason.EndDate:yyyy-MM-dd}");
            }

            var existingCultivation = await _unitOfWork.Repository<PlotCultivation>()
                .FindAsync(pc => pc.PlotId == request.PlotId && pc.YearSeasonId == request.YearSeasonId);

            if (existingCultivation?.FarmerSelectionDate != null)
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

            if (!riceVarietySeason.IsRecommended)
            {
                validation.Recommendations.Add(new ValidationRecommendation
                {
                    Title = "Not Recommended",
                    Description = "This variety is not recommended for this season. Consider choosing a recommended variety for better results.",
                    Type = "Warning"
                });
            }

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

    private static void AddError(CultivationValidationDto validation, string code, string message) =>
        validation.Errors.Add(new ValidationIssue
        {
            Code = code,
            Message = message,
            Severity = "Error"
        });

    private static Result<CultivationValidationDto> FailureResult(
        CultivationValidationDto validation,
        string code,
        string message)
    {
        AddError(validation, code, message);
        return Result<CultivationValidationDto>.Success(validation);
    }
}

