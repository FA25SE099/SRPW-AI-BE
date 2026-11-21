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

namespace RiceProduction.Application.PlotFeature.Commands.CreatePlot
{
    public class CreatePlotCommandHandler : IRequestHandler<CreatePlotCommand, Result<PlotResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly ILogger<CreatePlotCommandHandler> _logger;

        public CreatePlotCommandHandler(
            IUnitOfWork unitOfWork,
            IMediator mediator,
            ILogger<CreatePlotCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<PlotResponse>> Handle(
            CreatePlotCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(
                    request.FarmerId,
                    cancellationToken);

                if (farmer == null)
                {
                    return Result<PlotResponse>.Failure(
                        $"Farmer with ID '{request.FarmerId}' not found");
                }

                var plotRepo = _unitOfWork.Repository<Plot>();
                var existingPlot = await plotRepo.FindAsync(p =>
                    p.SoThua == request.SoThua &&
                    p.SoTo == request.SoTo &&
                    p.FarmerId == request.FarmerId);

                if (existingPlot != null)
                {
                    return Result<PlotResponse>.Failure(
                        $"Plot with SoThua={request.SoThua} and SoTo={request.SoTo} already exists for this farmer");
                }

                var geometryFactory = new GeometryFactory(new PrecisionModel(), PlotGeometryHelper.Wgs84Srid);
                var plotId = await plotRepo.GenerateNewGuid(Guid.NewGuid());
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

                await plotRepo.AddAsync(newPlot);

                PlotCultivation? plotCultivation = null;
                if (!string.IsNullOrWhiteSpace(request.RiceVarietyName))
                {
                    plotCultivation = await CreatePlotCultivationAsync(
                        plotId,
                        request.Area!.Value,
                        request.RiceVarietyName);
                }

                await _unitOfWork.CompleteAsync();

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
            string riceVarietyName)
        {
            try
            {
                var riceVariety = await _unitOfWork.Repository<RiceVariety>().FindAsync(
                    rv => rv.VarietyName == riceVarietyName);

                if (riceVariety == null)
                {
                    _logger.LogWarning(
                        "Rice variety '{RiceVarietyName}' not found, skipping cultivation creation",
                        riceVarietyName);
                    return null;
                }

                var allSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
                var resolvedSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, DateTime.Now);
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

                await _unitOfWork.Repository<PlotCultivation>().AddAsync(plotCultivation);

                var firstVersion = new CultivationVersion
                {
                    PlotCultivationId = plotCultivationId,
                    VersionName = "0",
                    VersionOrder = 1,
                    IsActive = true,
                    Reason = "Created during plot creation",
                    ActivatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Repository<CultivationVersion>().AddAsync(firstVersion);

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

