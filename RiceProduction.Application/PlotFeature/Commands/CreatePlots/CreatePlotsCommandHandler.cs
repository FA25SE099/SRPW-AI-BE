using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
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

                // Validate all farmers exist first
                var farmerIds = request.Plots.Select(p => p.FarmerId).Distinct().ToList();
                var farmers = await _unitOfWork.FarmerRepository.ListAsync(
                    f => farmerIds.Contains(f.Id));
                var farmerLookup = farmers.ToDictionary(f => f.Id);

                var validationErrors = new List<string>();
                for (int i = 0; i < request.Plots.Count; i++)
                {
                    var plot = request.Plots[i];
                    var plotIndex = i + 1;

                    if (!farmerLookup.ContainsKey(plot.FarmerId))
                    {
                        validationErrors.Add(
                            $"Plot #{plotIndex}: Farmer with ID '{plot.FarmerId}' not found");
                    }
                }

                if (validationErrors.Any())
                {
                    return Result<List<PlotResponse>>.Failure(
                        $"Validation failed:\n{string.Join("\n", validationErrors)}");
                }

                // Check for duplicates
                var plotRepo = _unitOfWork.Repository<Plot>();
                var existingPlots = await plotRepo.ListAsync(p => 
                    farmerIds.Contains(p.FarmerId));
                var existingPlotKeys = existingPlots
                    .Select(p => $"{p.FarmerId}_{p.SoThua}_{p.SoTo}")
                    .ToHashSet();

                // Get rice varieties if needed
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

                // Get current season
                var (currentSeason, currentYear) = await GetCurrentSeasonAndYear(cancellationToken);

                // Create plots
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                var coordinates = new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(0, 0.001),
                    new Coordinate(0.001, 0.001),
                    new Coordinate(0.001, 0),
                    new Coordinate(0, 0)
                };
                var defaultBoundary = geometryFactory.CreatePolygon(coordinates);

                var plotsToCreate = new List<Plot>();
                var cultivationsToCreate = new List<PlotCultivation>();
                var versionsToCreate = new List<CultivationVersion>();
                var skippedPlots = new List<string>();

                foreach (var plotRequest in request.Plots)
                {
                    var plotKey = $"{plotRequest.FarmerId}_{plotRequest.SoThua}_{plotRequest.SoTo}";

                    // Skip duplicates
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
                    var newPlot = new Plot
                    {
                        Id = plotId,
                        SoThua = plotRequest.SoThua,
                        SoTo = plotRequest.SoTo,
                        Area = plotRequest.Area!.Value,
                        FarmerId = plotRequest.FarmerId,
                        SoilType = plotRequest.SoilType,
                        Status = PlotStatus.PendingPolygon,
                        Boundary = defaultBoundary
                    };

                    plotsToCreate.Add(newPlot);

                    // Create cultivation if rice variety specified
                    if (!string.IsNullOrWhiteSpace(plotRequest.RiceVarietyName) &&
                        varietyLookup.TryGetValue(plotRequest.RiceVarietyName, out var riceVariety) &&
                        currentSeason != null)
                    {
                        var cultivationId = Guid.NewGuid();
                        var cultivation = new PlotCultivation
                        {
                            Id = cultivationId,
                            PlotId = plotId,
                            SeasonId = currentSeason.Id,
                            RiceVarietyId = riceVariety.Id,
                            PlantingDate = DateTime.UtcNow,
                            Area = plotRequest.Area!.Value,
                            Status = CultivationStatus.Planned
                        };

                        cultivationsToCreate.Add(cultivation);

                        var version = new CultivationVersion
                        {
                            PlotCultivationId = cultivationId,
                            VersionName = "0",
                            VersionOrder = 1,
                            IsActive = true,
                            Reason = "Created during bulk plot creation",
                            ActivatedAt = DateTime.UtcNow
                        };

                        versionsToCreate.Add(version);
                    }
                }

                if (!plotsToCreate.Any())
                {
                    return Result<List<PlotResponse>>.Failure(
                        "No plots to create. All plots already exist or validation failed.");
                }

                // Save everything
                await plotRepo.AddRangeAsync(plotsToCreate);

                if (cultivationsToCreate.Any())
                {
                    var cultivationRepo = _unitOfWork.Repository<PlotCultivation>();
                    await cultivationRepo.AddRangeAsync(cultivationsToCreate);
                    
                    _logger.LogInformation(
                        "Creating {Count} PlotCultivation records for season {SeasonName} {Year}",
                        cultivationsToCreate.Count,
                        currentSeason?.SeasonName,
                        currentYear);
                }

                if (versionsToCreate.Any())
                {
                    var versionRepo = _unitOfWork.Repository<CultivationVersion>();
                    await versionRepo.AddRangeAsync(versionsToCreate);
                    
                    _logger.LogInformation(
                        "Creating {Count} CultivationVersion records",
                        versionsToCreate.Count);
                }

                await _unitOfWork.CompleteAsync();

                // Create response
                var plotResponses = new List<PlotResponse>();
                foreach (var plot in plotsToCreate)
                {
                    var farmer = farmerLookup[plot.FarmerId];
                    plotResponses.Add(new PlotResponse
                    {
                        PlotId = plot.Id,
                        SoThua = plot.SoThua,
                        SoTo = plot.SoTo,
                        Area = plot.Area,
                        FarmerId = plot.FarmerId,
                        FarmerName = farmer.FullName ?? string.Empty,
                        SoilType = plot.SoilType,
                        Status = plot.Status,
                        GroupId = plot.GroupPlots.FirstOrDefault()?.GroupId
                    });
                }

                // Publish event for polygon assignment (non-blocking)
                if (plotResponses.Any())
                {
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
                }

                var message = $"Successfully created {plotResponses.Count} plot(s)";
                if (cultivationsToCreate.Any())
                {
                    message += $" with {cultivationsToCreate.Count} cultivation record(s)";
                }
                if (skippedPlots.Any())
                {
                    message += $". Skipped {skippedPlots.Count} duplicate(s): {string.Join(", ", skippedPlots)}";
                }

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

        private async Task<(Season? season, int year)> GetCurrentSeasonAndYear(
            CancellationToken cancellationToken)
        {
            var today = DateTime.Now;
            var currentMonth = today.Month;
            var currentDay = today.Day;

            var allSeasons = await _unitOfWork.Repository<Season>()
                .ListAsync(_ => true);

            foreach (var season in allSeasons)
            {
                if (IsDateInSeasonRange(currentMonth, currentDay, season.StartDate, season.EndDate))
                {
                    var startParts = season.StartDate.Split('/');
                    int startMonth = int.Parse(startParts[0]);

                    int year = today.Year;
                    if (currentMonth < startMonth && startMonth > 6)
                    {
                        year--;
                    }

                    return (season, year);
                }
            }

            return (null, today.Year);
        }

        private bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr)
        {
            try
            {
                var startParts = startDateStr.Split('/');
                var endParts = endDateStr.Split('/');

                int startMonth = int.Parse(startParts[0]);
                int startDay = int.Parse(startParts[1]);
                int endMonth = int.Parse(endParts[0]);
                int endDay = int.Parse(endParts[1]);

                int currentDate = month * 100 + day;
                int seasonStart = startMonth * 100 + startDay;
                int seasonEnd = endMonth * 100 + endDay;

                if (seasonStart > seasonEnd)
                {
                    return currentDate >= seasonStart || currentDate <= seasonEnd;
                }
                else
                {
                    return currentDate >= seasonStart && currentDate <= seasonEnd;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

