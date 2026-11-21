using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Queries.ValidateProductionPlanAgainstYearSeason;

public class ValidateProductionPlanAgainstYearSeasonQueryHandler 
    : IRequestHandler<ValidateProductionPlanAgainstYearSeasonQuery, Result<ProductionPlanValidationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ValidateProductionPlanAgainstYearSeasonQueryHandler> _logger;

    public ValidateProductionPlanAgainstYearSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<ValidateProductionPlanAgainstYearSeasonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ProductionPlanValidationDto>> Handle(
        ValidateProductionPlanAgainstYearSeasonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var validation = new ProductionPlanValidationDto
            {
                IsValid = true,
                Errors = new List<ValidationIssue>(),
                Warnings = new List<ValidationIssue>()
            };

            var group = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .AsNoTracking()
                .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys.Season)
                .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

            if (group == null)
            {
                AddError(validation, "GROUP_NOT_FOUND", $"Group with ID {request.GroupId} not found");
                return Result<ProductionPlanValidationDto>.Success(validation);
            }

            if (group.YearSeasonId == null || group.YearSeason == null)
            {
                AddError(validation, "YEARSEASON_MISSING", "Group is not assigned to a YearSeason. Cannot create production plan.");
                return Result<ProductionPlanValidationDto>.Success(validation);
            }

            var yearSeason = group.YearSeason;
            var now = DateTime.UtcNow;
            var basePlantingDate = DateTime.SpecifyKind(request.BasePlantingDate, DateTimeKind.Utc);

            validation.YearSeasonContext = BuildYearSeasonContext(yearSeason, group, now);
            ValidateYearSeasonRules(validation, yearSeason, group, basePlantingDate, now);

            _logger.LogInformation(
                "Validated production plan for group {GroupId} against YearSeason {YearSeasonId}. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                request.GroupId, yearSeason.Id, validation.IsValid, validation.Errors.Count, validation.Warnings.Count);

            return Result<ProductionPlanValidationDto>.Success(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating production plan against YearSeason for group {GroupId}", request.GroupId);
            return Result<ProductionPlanValidationDto>.Failure($"Error validating production plan: {ex.Message}");
        }
    }

    private static YearSeasonValidationContext BuildYearSeasonContext(
        YearSeason yearSeason,
        Group group,
        DateTime now) => new()
    {
        YearSeasonId = yearSeason.Id,
        SeasonName = yearSeason.Season?.SeasonName ?? "Unknown",
        Year = yearSeason.Year,
        Status = yearSeason.Status.ToString(),
        StartDate = yearSeason.StartDate,
        EndDate = yearSeason.EndDate,
        PlanningWindowStart = yearSeason.PlanningWindowStart,
        PlanningWindowEnd = yearSeason.PlanningWindowEnd,
        AllowedPlantingFlexibilityDays = yearSeason.AllowedPlantingFlexibilityDays,
        GroupPlantingDate = group.PlantingDate,
        DaysUntilPlanningWindowEnd = yearSeason.PlanningWindowEnd.HasValue
            ? (int)(yearSeason.PlanningWindowEnd.Value - now).TotalDays
            : null,
        DaysUntilSeasonStart = (int)(yearSeason.StartDate - now).TotalDays,
        IsPlanningWindowOpen = (!yearSeason.PlanningWindowStart.HasValue || now >= yearSeason.PlanningWindowStart.Value) &&
                               (!yearSeason.PlanningWindowEnd.HasValue || now <= yearSeason.PlanningWindowEnd.Value)
    };

    private static void ValidateYearSeasonRules(
        ProductionPlanValidationDto validation,
        YearSeason yearSeason,
        Group group,
        DateTime basePlantingDate,
        DateTime now)
    {
        if (yearSeason.Status == SeasonStatus.Draft)
        {
            AddError(validation, "YEARSEASON_DRAFT", "YearSeason is still in Draft status. Planning window has not opened yet.");
        }

        if (yearSeason.Status == SeasonStatus.Completed)
        {
            AddError(validation, "YEARSEASON_COMPLETED", "YearSeason has completed. Cannot create new production plans.");
        }

        if (yearSeason.PlanningWindowStart.HasValue && now < yearSeason.PlanningWindowStart.Value)
        {
            AddError(
                validation,
                "PLANNING_WINDOW_NOT_OPEN",
                $"Planning window has not opened yet. Opens on {yearSeason.PlanningWindowStart.Value:yyyy-MM-dd HH:mm}");
        }

        if (yearSeason.PlanningWindowEnd.HasValue)
        {
            if (now > yearSeason.PlanningWindowEnd.Value)
            {
                AddWarning(
                    validation,
                    "PLANNING_WINDOW_CLOSED",
                    $"Planning window closed on {yearSeason.PlanningWindowEnd.Value:yyyy-MM-dd HH:mm}. Late submission may require special approval.");
            }
            else
            {
                var daysRemaining = (yearSeason.PlanningWindowEnd.Value - now).TotalDays;
                if (daysRemaining is > 0 and <= 3)
                {
                    AddWarning(
                        validation,
                        "PLANNING_WINDOW_CLOSING",
                        $"Planning window closes in {Math.Ceiling(daysRemaining)} days on {yearSeason.PlanningWindowEnd.Value:yyyy-MM-dd HH:mm}");
                }
            }
        }

        if (basePlantingDate < yearSeason.StartDate || basePlantingDate > yearSeason.EndDate)
        {
            AddError(
                validation,
                "PLANTING_DATE_OUT_OF_RANGE",
                $"BasePlantingDate ({basePlantingDate:yyyy-MM-dd}) must be within YearSeason period ({yearSeason.StartDate:yyyy-MM-dd} to {yearSeason.EndDate:yyyy-MM-dd})");
        }

        if (basePlantingDate < now)
        {
            AddWarning(
                validation,
                "PLANTING_DATE_IN_PAST",
                $"BasePlantingDate ({basePlantingDate:yyyy-MM-dd}) is in the past");
        }

        if (group.PlantingDate.HasValue && yearSeason.AllowedPlantingFlexibilityDays > 0)
        {
            var daysDiff = Math.Abs((basePlantingDate - group.PlantingDate.Value).Days);
            if (daysDiff > yearSeason.AllowedPlantingFlexibilityDays)
            {
                AddWarning(
                    validation,
                    "PLANTING_DATE_FLEXIBILITY_EXCEEDED",
                    $"BasePlantingDate differs from group's median planting date by {daysDiff} days (allowed: {yearSeason.AllowedPlantingFlexibilityDays} days). Group median: {group.PlantingDate.Value:yyyy-MM-dd}");
            }
        }

        if (now > yearSeason.StartDate)
        {
            AddWarning(
                validation,
                "SEASON_ALREADY_STARTED",
                $"YearSeason has already started on {yearSeason.StartDate:yyyy-MM-dd}. Creating plans after season start may cause delays.");
        }
    }

    private static void AddError(ProductionPlanValidationDto validation, string code, string message)
    {
        validation.IsValid = false;
        validation.Errors.Add(new ValidationIssue
        {
            Code = code,
            Message = message,
            Severity = "Error"
        });
    }

    private static void AddWarning(ProductionPlanValidationDto validation, string code, string message)
    {
        validation.Warnings.Add(new ValidationIssue
        {
            Code = code,
            Message = message,
            Severity = "Warning"
        });
    }
}

