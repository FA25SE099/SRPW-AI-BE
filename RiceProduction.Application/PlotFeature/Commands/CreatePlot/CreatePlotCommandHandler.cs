using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
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
                // Validate farmer exists
                var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(
                    request.FarmerId, 
                    cancellationToken);
                    
                if (farmer == null)
                {
                    return Result<PlotResponse>.Failure(
                        $"Farmer with ID '{request.FarmerId}' not found");
                }

                // Check for duplicate plot
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

                // Create default boundary (will be updated later by supervisor)
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

                // Create plot
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
                    Boundary = defaultBoundary
                };

                await plotRepo.AddAsync(newPlot);

                // Create PlotCultivation if rice variety specified
                PlotCultivation? plotCultivation = null;
                CultivationVersion? cultivationVersion = null;
                
                if (!string.IsNullOrWhiteSpace(request.RiceVarietyName))
                {
                    var (cultivation, version) = await CreatePlotCultivationAsync(
                        plotId,
                        request.Area!.Value,
                        request.RiceVarietyName,
                        cancellationToken);
                        
                    plotCultivation = cultivation;
                    cultivationVersion = version;
                }

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation(
                    "Created plot {PlotId} (SoThua:{SoThua}, SoTo:{SoTo}) for farmer {FarmerId}",
                    newPlot.Id, newPlot.SoThua, newPlot.SoTo, farmer.Id);

                // Create response
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

        private async Task<(PlotCultivation?, CultivationVersion?)> CreatePlotCultivationAsync(
            Guid plotId,
            decimal area,
            string riceVarietyName,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get rice variety
                var riceVarietyRepo = _unitOfWork.Repository<RiceVariety>();
                var riceVariety = await riceVarietyRepo.FindAsync(
                    rv => rv.VarietyName == riceVarietyName);

                if (riceVariety == null)
                {
                    _logger.LogWarning(
                        "Rice variety '{RiceVarietyName}' not found, skipping cultivation creation",
                        riceVarietyName);
                    return (null, null);
                }

                // Get current season
                var (currentSeason, currentYear) = await GetCurrentSeasonAndYear(cancellationToken);
                if (currentSeason == null)
                {
                    _logger.LogWarning(
                        "No current season found, skipping cultivation creation");
                    return (null, null);
                }

                // Create PlotCultivation
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

                var plotCultivationRepo = _unitOfWork.Repository<PlotCultivation>();
                await plotCultivationRepo.AddAsync(plotCultivation);

                // Create first version
                var firstVersion = new CultivationVersion
                {
                    PlotCultivationId = plotCultivationId,
                    VersionName = "Initial Version",
                    VersionOrder = 1,
                    IsActive = true,
                    Reason = "Created during plot creation",
                    ActivatedAt = DateTime.UtcNow
                };

                var cultivationVersionRepo = _unitOfWork.Repository<CultivationVersion>();
                await cultivationVersionRepo.AddAsync(firstVersion);

                _logger.LogInformation(
                    "Created cultivation for plot {PlotId} with variety '{RiceVarietyName}' in season {SeasonName} {Year}",
                    plotId, riceVarietyName, currentSeason.SeasonName, currentYear);

                return (plotCultivation, firstVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating plot cultivation");
                return (null, null);
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

