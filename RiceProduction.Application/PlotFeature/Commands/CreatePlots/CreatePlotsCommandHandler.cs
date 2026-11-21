using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Application.PlotFeature.Events;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Commands.CreatePlots
{
    public class CreatePlotsCommandHandler : IRequestHandler<CreatePlotsCommand, Result<List<PlotResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly ILogger<CreatePlotsCommandHandler> _logger;

        public CreatePlotsCommandHandler(
            IUnitOfWork unitOfWork,
            IMediator mediator,
            ILogger<CreatePlotsCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<List<PlotResponse>>> Handle(
            CreatePlotsCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting bulk plot creation for {Count} plots", request.Plots.Count);

                var farmerIds = request.Plots.Select(p => p.FarmerId).Distinct().ToList();
                var farmers = await _unitOfWork.FarmerRepository.ListAsync(
                    f => farmerIds.Contains(f.Id));
                var farmerLookup = farmers.ToDictionary(f => f.Id);

                var validationErrors = new List<string>();
                for (int i = 0; i < request.Plots.Count; i++)
                {
                    var plot = request.Plots[i];
                    if (!farmerLookup.ContainsKey(plot.FarmerId))
                    {
                        validationErrors.Add(
                            $"Plot #{i + 1}: Farmer with ID '{plot.FarmerId}' not found");
                    }
                }

                if (validationErrors.Any())
                {
                    return Result<List<PlotResponse>>.Failure(
                        $"Validation failed:\n{string.Join("\n", validationErrors)}");
                }

                var plotRepo = _unitOfWork.Repository<Plot>();
                var existingPlots = await plotRepo.ListAsync(p =>
                    farmerIds.Contains(p.FarmerId));
                var existingPlotKeys = existingPlots
                    .Select(p => $"{p.FarmerId}_{p.SoThua}_{p.SoTo}")
                    .ToHashSet();

                var riceVarietyNames = request.Plots
                    .Where(p => !string.IsNullOrWhiteSpace(p.RiceVarietyName))
                    .Select(p => p.RiceVarietyName!)
                    .Distinct()
                    .ToList();

                var riceVarieties = riceVarietyNames.Any()
                    ? await _unitOfWork.Repository<RiceVariety>()
                        .ListAsync(rv => riceVarietyNames.Contains(rv.VarietyName))
                    : new List<RiceVariety>();
                var varietyLookup = riceVarieties.ToDictionary(rv => rv.VarietyName);

                var allSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
                var today = DateTime.Now;
                var resolvedSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, today);
                var currentSeason = resolvedSeason?.Season;
                var currentYear = resolvedSeason?.Year ?? today.Year;

                var geometryFactory = new GeometryFactory(new PrecisionModel(), PlotGeometryHelper.Wgs84Srid);
                var defaultBoundary = PlotGeometryHelper.CreateDefaultBoundary(geometryFactory);

                var plotsToCreate = new List<Plot>();
                var cultivationsToCreate = new List<PlotCultivation>();
                var versionsToCreate = new List<CultivationVersion>();
                var skippedPlots = new List<string>();

                foreach (var plotRequest in request.Plots)
                {
                    var plotKey = $"{plotRequest.FarmerId}_{plotRequest.SoThua}_{plotRequest.SoTo}";

                    if (existingPlotKeys.Contains(plotKey))
                    {
                        skippedPlots.Add(
                            $"SoThua:{plotRequest.SoThua}, SoTo:{plotRequest.SoTo} (already exists)");
                        _logger.LogWarning(
                            "Skipping duplicate plot: Farmer={FarmerId}, SoThua={SoThua}, SoTo={SoTo}",
                            plotRequest.FarmerId, plotRequest.SoThua, plotRequest.SoTo);
                        continue;
                    }

                    var plotId = await plotRepo.GenerateNewGuid(Guid.NewGuid());
                    plotsToCreate.Add(new Plot
                    {
                        Id = plotId,
                        SoThua = plotRequest.SoThua,
                        SoTo = plotRequest.SoTo,
                        Area = plotRequest.Area!.Value,
                        FarmerId = plotRequest.FarmerId,
                        SoilType = plotRequest.SoilType,
                        Status = PlotStatus.PendingPolygon,
                        Boundary = defaultBoundary
                    });

                    if (string.IsNullOrWhiteSpace(plotRequest.RiceVarietyName) ||
                        !varietyLookup.TryGetValue(plotRequest.RiceVarietyName, out var riceVariety) ||
                        currentSeason == null)
                    {
                        continue;
                    }

                    var cultivationId = Guid.NewGuid();
                    cultivationsToCreate.Add(new PlotCultivation
                    {
                        Id = cultivationId,
                        PlotId = plotId,
                        SeasonId = currentSeason.Id,
                        RiceVarietyId = riceVariety.Id,
                        PlantingDate = DateTime.UtcNow,
                        Area = plotRequest.Area!.Value,
                        Status = CultivationStatus.Planned
                    });

                    versionsToCreate.Add(new CultivationVersion
                    {
                        PlotCultivationId = cultivationId,
                        VersionName = "0",
                        VersionOrder = 1,
                        IsActive = true,
                        Reason = "Created during bulk plot creation",
                        ActivatedAt = DateTime.UtcNow
                    });
                }

                if (!plotsToCreate.Any())
                {
                    return Result<List<PlotResponse>>.Failure(
                        "No plots to create. All plots already exist or validation failed.");
                }

                await plotRepo.AddRangeAsync(plotsToCreate);

                if (cultivationsToCreate.Any())
                {
                    await _unitOfWork.Repository<PlotCultivation>().AddRangeAsync(cultivationsToCreate);

                    _logger.LogInformation(
                        "Creating {Count} PlotCultivation records for season {SeasonName} {Year}",
                        cultivationsToCreate.Count,
                        currentSeason?.SeasonName,
                        currentYear);
                }

                if (versionsToCreate.Any())
                {
                    await _unitOfWork.Repository<CultivationVersion>().AddRangeAsync(versionsToCreate);

                    _logger.LogInformation(
                        "Creating {Count} CultivationVersion records",
                        versionsToCreate.Count);
                }

                await _unitOfWork.CompleteAsync();

                var plotResponses = plotsToCreate
                    .Select(plot => new PlotResponse
                    {
                        PlotId = plot.Id,
                        SoThua = plot.SoThua,
                        SoTo = plot.SoTo,
                        Area = plot.Area,
                        FarmerId = plot.FarmerId,
                        FarmerName = farmerLookup[plot.FarmerId].FullName ?? string.Empty,
                        SoilType = plot.SoilType,
                        Status = plot.Status,
                        GroupId = plot.GroupPlots.FirstOrDefault()?.GroupId
                    })
                    .ToList();

                // Publish event for polygon assignment (non-blocking)
                await _mediator.Publish(new PlotImportedEvent
                {
                    ImportedPlots = plotResponses,
                    ClusterManagerId = request.ClusterManagerId,
                    ImportedAt = DateTime.UtcNow,
                    TotalPlotsImported = plotResponses.Count
                }, cancellationToken);

                _logger.LogInformation(
                    "Published PlotImportedEvent for {Count} plots",
                    plotResponses.Count);

                var message = BuildSuccessMessage(
                    plotResponses.Count,
                    cultivationsToCreate.Count,
                    versionsToCreate.Count,
                    currentSeason?.SeasonName,
                    currentYear,
                    skippedPlots);

                _logger.LogInformation("Bulk plot creation completed: {Message}", message);

                return Result<List<PlotResponse>>.Success(plotResponses, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating plots in bulk");
                return Result<List<PlotResponse>>.Failure(
                    $"Failed to create plots: {ex.Message}");
            }
        }

        private static string BuildSuccessMessage(
            int plotCount,
            int cultivationCount,
            int versionCount,
            string? seasonName,
            int currentYear,
            List<string> skippedPlots)
        {
            var message = $"Successfully created {plotCount} plot(s)";
            if (cultivationCount > 0)
            {
                message += $" with {cultivationCount} cultivation record(s)";
                if (versionCount > 0)
                {
                    message += $" and {versionCount} initial version(s)";
                }
                message += $" for {seasonName} {currentYear}";
            }

            if (skippedPlots.Any())
            {
                message += $". Skipped {skippedPlots.Count} duplicate(s): {string.Join(", ", skippedPlots)}";
            }

            return message;
        }
    }
}

