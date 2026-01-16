using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Queries.ValidateCultivationPreferences;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FarmerFeature.Commands.SelectCultivationPreferences;

public class SelectCultivationPreferencesCommandHandler 
    : IRequestHandler<SelectCultivationPreferencesCommand, Result<CultivationPreferenceDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<SelectCultivationPreferencesCommandHandler> _logger;

    public SelectCultivationPreferencesCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<SelectCultivationPreferencesCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<CultivationPreferenceDto>> Handle(
        SelectCultivationPreferencesCommand request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Validate using the validation query
            var validationQuery = new ValidateCultivationPreferencesQuery
            {
                PlotId = request.PlotId,
                YearSeasonId = request.YearSeasonId,
                RiceVarietyId = request.RiceVarietyId,
                PreferredPlantingDate = request.PreferredPlantingDate
            };

            var validationResult = await _mediator.Send(validationQuery, cancellationToken);
            
            if (!validationResult.Succeeded)
            {
                return Result<CultivationPreferenceDto>.Failure(validationResult.Message);
            }

            var validation = validationResult.Data;
            if (!validation.IsValid)
            {
                var errorMessages = string.Join("; ", validation.Errors.Select(e => e.Message));
                return Result<CultivationPreferenceDto>.Failure($"Validation failed: {errorMessages}");
            }

            // 2. Get YearSeason to get SeasonId
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Include(ys => ys.Season)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<CultivationPreferenceDto>.Failure("Year Season not found");
            }

            // 3. Get Plot for additional data
            var plot = await _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.Id == request.PlotId, cancellationToken);

            if (plot == null)
            {
                return Result<CultivationPreferenceDto>.Failure("Plot not found");
            }

            // 4. Verify ownership
            if (_currentUser.Id.HasValue && plot.FarmerId != _currentUser.Id.Value)
            {
                return Result<CultivationPreferenceDto>.Failure("You do not have permission to select cultivation for this plot");
            }

            // 5. Get RiceVariety for additional data
            var riceVariety = await _unitOfWork.Repository<RiceVariety>()
                .FindAsync(rv => rv.Id == request.RiceVarietyId);

            if (riceVariety == null)
            {
                return Result<CultivationPreferenceDto>.Failure("Rice variety not found");
            }

            // 6. Check for existing cultivation
            var existingCultivation = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .FirstOrDefaultAsync(pc => 
                    pc.PlotId == request.PlotId && 
                    pc.YearSeasonId == request.YearSeasonId, 
                    cancellationToken);

            PlotCultivation plotCultivation;

            if (existingCultivation != null)
            {
                // Update existing cultivation
                _logger.LogInformation(
                    "Updating existing cultivation selection for plot {PlotId}", 
                    request.PlotId);

                existingCultivation.RiceVarietyId = request.RiceVarietyId;
                existingCultivation.PlantingDate = request.PreferredPlantingDate;
                existingCultivation.FarmerSelectionDate = DateTime.UtcNow;
                existingCultivation.Status = CultivationStatus.Planned;

                _unitOfWork.Repository<PlotCultivation>().Update(existingCultivation);
                plotCultivation = existingCultivation;
            }
            else
            {
                // Create new cultivation
                _logger.LogInformation(
                    "Creating new cultivation selection for plot {PlotId}", 
                    request.PlotId);

                plotCultivation = new PlotCultivation
                {
                    PlotId = request.PlotId,
                    YearSeasonId = request.YearSeasonId,
                    SeasonId = yearSeason.SeasonId,
                    RiceVarietyId = request.RiceVarietyId,
                    PlantingDate = request.PreferredPlantingDate,
                    FarmerSelectionDate = DateTime.UtcNow,
                    Status = CultivationStatus.Planned,
                    Area = plot.Area
                };

                await _unitOfWork.Repository<PlotCultivation>().AddAsync(plotCultivation);
            }

            // 7. Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 8. Create initial CultivationVersion if it doesn't exist
            var cultivationVersionRepo = _unitOfWork.Repository<CultivationVersion>();
            var existingVersion = await cultivationVersionRepo.GetQueryable()
                .FirstOrDefaultAsync(v => 
                    v.PlotCultivationId == plotCultivation.Id && 
                    v.VersionName == "0", 
                    cancellationToken);

            if (existingVersion == null)
            {
                var firstVersion = new CultivationVersion
                {
                    PlotCultivationId = plotCultivation.Id,
                    VersionName = "0",
                    VersionOrder = 1,
                    IsActive = true,
                    Reason = existingCultivation != null 
                        ? $"Farmer updated cultivation selection on {DateTime.UtcNow:yyyy-MM-dd}" 
                        : $"Created during farmer cultivation selection for {yearSeason.Season?.SeasonName ?? "season"} {yearSeason.Year}",
                    ActivatedAt = DateTime.UtcNow
                };

                await cultivationVersionRepo.AddAsync(firstVersion);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Created initial CultivationVersion '0' for PlotCultivation {PlotCultivationId}",
                    plotCultivation.Id);
            }

            // 9. Create response DTO
            var responseDto = new CultivationPreferenceDto
            {
                PlotCultivationId = plotCultivation.Id,
                PlotId = plotCultivation.PlotId,
                PlotName = "",
                YearSeasonId = plotCultivation.YearSeasonId.Value,
                RiceVarietyId = plotCultivation.RiceVarietyId,
                RiceVarietyName = riceVariety.VarietyName,
                PlantingDate = plotCultivation.PlantingDate,
                EstimatedHarvestDate = validation.EstimatedHarvestDate,
                GrowthDurationDays = validation.GrowthDurationDays,
                ExpectedYield = validation.ExpectedYield,
                SelectionDate = plotCultivation.FarmerSelectionDate.Value,
                Status = plotCultivation.Status.ToString()
            };

            _logger.LogInformation(
                "Successfully saved cultivation preferences for plot {PlotId}, variety {RiceVarietyId}", 
                request.PlotId, request.RiceVarietyId);

            return Result<CultivationPreferenceDto>.Success(responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error selecting cultivation preferences for plot {PlotId}", 
                request.PlotId);
            return Result<CultivationPreferenceDto>.Failure(
                "An error occurred while saving your selection");
        }
    }
}

