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

            // Load group with YearSeason
            var group = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys.Season)
                .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

            if (group == null)
            {
                validation.IsValid = false;
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "GROUP_NOT_FOUND",
                    Message = $"Group with ID {request.GroupId} not found",
                    Severity = "Error"
                });
                return Result<ProductionPlanValidationDto>.Success(validation);
            }

            // Check if group has YearSeason
            if (group.YearSeasonId == null || group.YearSeason == null)
            {
                validation.IsValid = false;
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "YEARSEASON_MISSING",
                    Message = "Group is not assigned to a YearSeason. Cannot create production plan.",
                    Severity = "Error"
                });
                return Result<ProductionPlanValidationDto>.Success(validation);
            }

            var yearSeason = group.YearSeason;
            var now = DateTime.UtcNow;
            var basePlantingDate = DateTime.SpecifyKind(request.BasePlantingDate, DateTimeKind.Utc);

            // Build context
            validation.YearSeasonContext = new YearSeasonValidationContext
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

            // Validate YearSeason Status
            if (yearSeason.Status == SeasonStatus.Draft)
            {
                validation.IsValid = false;
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "YEARSEASON_DRAFT",
                    Message = "YearSeason is still in Draft status. Planning window has not opened yet.",
                    Severity = "Error"
                });
            }

            if (yearSeason.Status == SeasonStatus.Completed)
            {
                validation.IsValid = false;
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "YEARSEASON_COMPLETED",
                    Message = "YearSeason has completed. Cannot create new production plans.",
                    Severity = "Error"
                });
            }

            // Validate Planning Window Start
            if (yearSeason.PlanningWindowStart.HasValue && now < yearSeason.PlanningWindowStart.Value)
            {
                validation.IsValid = false;
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "PLANNING_WINDOW_NOT_OPEN",
                    Message = $"Planning window has not opened yet. Opens on {yearSeason.PlanningWindowStart.Value:yyyy-MM-dd HH:mm}",
                    Severity = "Error"
                });
            }

            // Validate Planning Window End
            if (yearSeason.PlanningWindowEnd.HasValue && now > yearSeason.PlanningWindowEnd.Value)
            {
                validation.Warnings.Add(new ValidationIssue
                {
                    Code = "PLANNING_WINDOW_CLOSED",
                    Message = $"Planning window closed on {yearSeason.PlanningWindowEnd.Value:yyyy-MM-dd HH:mm}. Late submission may require special approval.",
                    Severity = "Warning"
                });
            }
            else if (yearSeason.PlanningWindowEnd.HasValue)
            {
                var daysRemaining = (yearSeason.PlanningWindowEnd.Value - now).TotalDays;
                if (daysRemaining <= 3 && daysRemaining > 0)
                {
                    validation.Warnings.Add(new ValidationIssue
                    {
                        Code = "PLANNING_WINDOW_CLOSING",
                        Message = $"Planning window closes in {Math.Ceiling(daysRemaining)} days on {yearSeason.PlanningWindowEnd.Value:yyyy-MM-dd HH:mm}",
                        Severity = "Warning"
                    });
                }
            }

            // Validate BasePlantingDate within Season
            if (basePlantingDate < yearSeason.StartDate || basePlantingDate > yearSeason.EndDate)
            {
                validation.IsValid = false;
                validation.Errors.Add(new ValidationIssue
                {
                    Code = "PLANTING_DATE_OUT_OF_RANGE",
                    Message = $"BasePlantingDate ({basePlantingDate:yyyy-MM-dd}) must be within YearSeason period ({yearSeason.StartDate:yyyy-MM-dd} to {yearSeason.EndDate:yyyy-MM-dd})",
                    Severity = "Error"
                });
            }

            // Validate Planting Date is in the future
            if (basePlantingDate < now)
            {
                validation.Warnings.Add(new ValidationIssue
                {
                    Code = "PLANTING_DATE_IN_PAST",
                    Message = $"BasePlantingDate ({basePlantingDate:yyyy-MM-dd}) is in the past",
                    Severity = "Warning"
                });
            }

            // Validate Planting Flexibility
            if (group.PlantingDate.HasValue && yearSeason.AllowedPlantingFlexibilityDays > 0)
            {
                var daysDiff = Math.Abs((basePlantingDate - group.PlantingDate.Value).Days);
                if (daysDiff > yearSeason.AllowedPlantingFlexibilityDays)
                {
                    validation.Warnings.Add(new ValidationIssue
                    {
                        Code = "PLANTING_DATE_FLEXIBILITY_EXCEEDED",
                        Message = $"BasePlantingDate differs from group's median planting date by {daysDiff} days (allowed: {yearSeason.AllowedPlantingFlexibilityDays} days). Group median: {group.PlantingDate.Value:yyyy-MM-dd}",
                        Severity = "Warning"
                    });
                }
            }

            // Check if season has already started
            if (now > yearSeason.StartDate)
            {
                validation.Warnings.Add(new ValidationIssue
                {
                    Code = "SEASON_ALREADY_STARTED",
                    Message = $"YearSeason has already started on {yearSeason.StartDate:yyyy-MM-dd}. Creating plans after season start may cause delays.",
                    Severity = "Warning"
                });
            }

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
}

