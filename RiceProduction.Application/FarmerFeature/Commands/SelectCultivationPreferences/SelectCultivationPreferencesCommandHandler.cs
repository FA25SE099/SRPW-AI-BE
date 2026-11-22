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
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<SelectCultivationPreferencesCommandHandler> _logger;

    public SelectCultivationPreferencesCommandHandler(
        IApplicationDbContext context,
        IUser currentUser,
        IMediator mediator,
        ILogger<SelectCultivationPreferencesCommandHandler> logger)
    {
        _context = context;
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

            var yearSeason = await _context.YearSeasons
                .AsNoTracking()
                .Include(ys => ys.Season)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<CultivationPreferenceDto>.Failure("Year Season not found");
            }

            var plot = await _context.Plots
                .AsNoTracking()
                .Where(p => p.Id == request.PlotId)
                .Select(p => new { p.Id, p.FarmerId, p.Area })
                .FirstOrDefaultAsync(cancellationToken);

            if (plot == null)
            {
                return Result<CultivationPreferenceDto>.Failure("Plot not found");
            }

            if (_currentUser.Id.HasValue && plot.FarmerId != _currentUser.Id.Value)
            {
                return Result<CultivationPreferenceDto>.Failure("You do not have permission to select cultivation for this plot");
            }

            var riceVariety = await _context.RiceVarieties
                .AsNoTracking()
                .Where(rv => rv.Id == request.RiceVarietyId)
                .Select(rv => new { rv.Id, rv.VarietyName })
                .FirstOrDefaultAsync(cancellationToken);

            if (riceVariety == null)
            {
                return Result<CultivationPreferenceDto>.Failure("Rice variety not found");
            }

            var (plotCultivation, isUpdate) = await UpsertPlotCultivationAsync(request, yearSeason, plot.Area, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await EnsureInitialCultivationVersionAsync(plotCultivation, yearSeason, isUpdate, cancellationToken);

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

    private async Task<(PlotCultivation Cultivation, bool IsUpdate)> UpsertPlotCultivationAsync(
        SelectCultivationPreferencesCommand request,
        YearSeason yearSeason,
        decimal plotArea,
        CancellationToken cancellationToken)
    {
        var existingCultivation = await _context.PlotCultivations
            .FirstOrDefaultAsync(pc =>
                pc.PlotId == request.PlotId &&
                pc.YearSeasonId == request.YearSeasonId,
                cancellationToken);

        if (existingCultivation != null)
        {
            _logger.LogInformation(
                "Updating existing cultivation selection for plot {PlotId}",
                request.PlotId);

            existingCultivation.RiceVarietyId = request.RiceVarietyId;
            existingCultivation.PlantingDate = request.PreferredPlantingDate;
            existingCultivation.FarmerSelectionDate = DateTime.UtcNow;
            existingCultivation.Status = CultivationStatus.Planned;

            _context.PlotCultivations.Update(existingCultivation);
            return (existingCultivation, true);
        }

        _logger.LogInformation(
            "Creating new cultivation selection for plot {PlotId}",
            request.PlotId);

        var plotCultivation = new PlotCultivation
        {
            PlotId = request.PlotId,
            YearSeasonId = request.YearSeasonId,
            SeasonId = yearSeason.SeasonId,
            RiceVarietyId = request.RiceVarietyId,
            PlantingDate = request.PreferredPlantingDate,
            FarmerSelectionDate = DateTime.UtcNow,
            Status = CultivationStatus.Planned,
            Area = plotArea
        };

        _context.PlotCultivations.Add(plotCultivation);
        return (plotCultivation, false);
    }

    private async Task EnsureInitialCultivationVersionAsync(
        PlotCultivation plotCultivation,
        YearSeason yearSeason,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        var existingVersion = await _context.CultivationVersions
            .AsNoTracking()
            .AnyAsync(v =>
                v.PlotCultivationId == plotCultivation.Id &&
                v.VersionName == "0",
                cancellationToken);

        if (existingVersion)
        {
            return;
        }

        var firstVersion = new CultivationVersion
        {
            PlotCultivationId = plotCultivation.Id,
            VersionName = "0",
            VersionOrder = 1,
            IsActive = true,
            Reason = isUpdate
                ? $"Farmer updated cultivation selection on {DateTime.UtcNow:yyyy-MM-dd}"
                : $"Created during farmer cultivation selection for {yearSeason.Season?.SeasonName ?? "season"} {yearSeason.Year}",
            ActivatedAt = DateTime.UtcNow
        };

        _context.CultivationVersions.Add(firstVersion);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created initial CultivationVersion '0' for PlotCultivation {PlotCultivationId}",
            plotCultivation.Id);
    }
}

