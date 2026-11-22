using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Application.PlotFeature.Events;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Commands.CreatePlot
{
    public class CreatePlotCommandHandler : IRequestHandler<CreatePlotCommand, Result<PlotResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<CreatePlotCommandHandler> _logger;

        public CreatePlotCommandHandler(
            IApplicationDbContext context,
            IMediator mediator,
            ILogger<CreatePlotCommandHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<PlotResponse>> Handle(
            CreatePlotCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var farmer = await _context.Farmers
                    .AsNoTracking()
                    .Select(f => new { f.Id, f.FullName })
                    .FirstOrDefaultAsync(f => f.Id == request.FarmerId, cancellationToken);

                if (farmer == null)
                {
                    return Result<PlotResponse>.Failure(
                        $"Farmer with ID '{request.FarmerId}' not found");
                }

                var existingPlot = await _context.Plots
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.SoThua == request.SoThua &&
                        p.SoTo == request.SoTo &&
                        p.FarmerId == request.FarmerId,
                        cancellationToken);

                if (existingPlot)
                {
                    return Result<PlotResponse>.Failure(
                        $"Plot with SoThua={request.SoThua} and SoTo={request.SoTo} already exists for this farmer");
                }

                var geometryFactory = new GeometryFactory(new PrecisionModel(), PlotGeometryHelper.Wgs84Srid);
                var plotId = Guid.NewGuid();
                var newPlot = new Plot
                {
                    Id = plotId,
                    SoThua = request.SoThua,
                    SoTo = request.SoTo,
                    Area = request.Area!.Value,
                    FarmerId = request.FarmerId,
                    SoilType = request.SoilType,
                    Status = PlotStatus.PendingPolygon,
                    // Placeholder boundary until a supervisor draws the actual polygon
                    Boundary = PlotGeometryHelper.CreateDefaultBoundary(geometryFactory)
                };

                _context.Plots.Add(newPlot);

                PlotCultivation? plotCultivation = null;
                if (!string.IsNullOrWhiteSpace(request.RiceVarietyName))
                {
                    plotCultivation = await CreatePlotCultivationAsync(
                        plotId,
                        request.Area!.Value,
                        request.RiceVarietyName,
                        cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Created plot {PlotId} (SoThua:{SoThua}, SoTo:{SoTo}) for farmer {FarmerId}",
                    newPlot.Id, newPlot.SoThua, newPlot.SoTo, farmer.Id);

                var plotResponse = new PlotResponse
                {
                    PlotId = newPlot.Id,
                    SoThua = newPlot.SoThua,
                    SoTo = newPlot.SoTo,
                    Area = newPlot.Area,
                    FarmerId = newPlot.FarmerId,
                    FarmerName = farmer.FullName ?? string.Empty,
                    SoilType = newPlot.SoilType,
                    Status = newPlot.Status,
                    GroupId = newPlot.GroupPlots.FirstOrDefault()?.GroupId
                };

                // Publish event for polygon assignment (non-blocking)
                await _mediator.Publish(new PlotImportedEvent
                {
                    ImportedPlots = new List<PlotResponse> { plotResponse },
                    ClusterManagerId = request.ClusterManagerId,
                    ImportedAt = DateTime.UtcNow,
                    TotalPlotsImported = 1
                }, cancellationToken);

                _logger.LogInformation(
                    "Published PlotImportedEvent for plot {PlotId}",
                    newPlot.Id);

                var message = $"Successfully created plot (SoThua:{newPlot.SoThua}, SoTo:{newPlot.SoTo})";
                if (plotCultivation != null)
                {
                    message += " with initial cultivation record";
                }

                return Result<PlotResponse>.Success(plotResponse, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating plot");
                return Result<PlotResponse>.Failure($"Failed to create plot: {ex.Message}");
            }
        }

        private async Task<PlotCultivation?> CreatePlotCultivationAsync(
            Guid plotId,
            decimal area,
            string riceVarietyName,
            CancellationToken cancellationToken)
        {
            try
            {
                var riceVariety = await _context.RiceVarieties
                    .AsNoTracking()
                    .FirstOrDefaultAsync(rv => rv.VarietyName == riceVarietyName, cancellationToken);

                if (riceVariety == null)
                {
                    _logger.LogWarning(
                        "Rice variety '{RiceVarietyName}' not found, skipping cultivation creation",
                        riceVarietyName);
                    return null;
                }

                var allSeasons = await _context.Seasons
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                var resolvedSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, DateTime.UtcNow);
                if (resolvedSeason == null)
                {
                    _logger.LogWarning(
                        "No current season found, skipping cultivation creation");
                    return null;
                }

                var (currentSeason, currentYear) = resolvedSeason.Value;

                var plotCultivationId = Guid.NewGuid();
                var plotCultivation = new PlotCultivation
                {
                    Id = plotCultivationId,
                    PlotId = plotId,
                    SeasonId = currentSeason.Id,
                    RiceVarietyId = riceVariety.Id,
                    PlantingDate = DateTime.UtcNow,
                    Area = area,
                    Status = CultivationStatus.Planned
                };

                _context.PlotCultivations.Add(plotCultivation);

                var firstVersion = new CultivationVersion
                {
                    PlotCultivationId = plotCultivationId,
                    VersionName = "0",
                    VersionOrder = 1,
                    IsActive = true,
                    Reason = "Created during plot creation",
                    ActivatedAt = DateTime.UtcNow
                };

                _context.CultivationVersions.Add(firstVersion);

                _logger.LogInformation(
                    "Created cultivation for plot {PlotId} with variety '{RiceVarietyName}' in season {SeasonName} {Year}",
                    plotId, riceVarietyName, currentSeason.SeasonName, currentYear);

                return plotCultivation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating plot cultivation");
                return null;
            }
        }
    }
}

