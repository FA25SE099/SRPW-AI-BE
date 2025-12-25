using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Application.PlotFeature.Events;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Commands.ImportExcel
{
    public class ImportPlotByExcelCommandHandler : IRequestHandler<ImportPlotByExcelCommand, Result<List<PlotResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;
        private readonly IMediator _mediator;
        private readonly ILogger<ImportPlotByExcelCommandHandler> _logger;

        public ImportPlotByExcelCommandHandler(
            IUnitOfWork unitOfWork, 
            IGenericExcel genericExcel, 
            IMediator mediator, 
            ILogger<ImportPlotByExcelCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<List<PlotResponse>>> Handle(
            ImportPlotByExcelCommand request, 
            CancellationToken cancellationToken)
        {
            try
            {
                // Read using the new PlotImportRow format
                var plotImportRows = await _genericExcel.ExcelToListT<PlotImportRow>(request.ExcelFile);
                if (plotImportRows == null || !plotImportRows.Any())
                {
                    return Result<List<PlotResponse>>.Failure(
                        "The uploaded Excel file is empty or invalid.");
                }

                var plotRepo = _unitOfWork.Repository<Plot>();
                var farmerRepo = _unitOfWork.FarmerRepository;
                var riceVarietyRepo = _unitOfWork.Repository<RiceVariety>();

                // Initialize WKT reader for polygon parsing
                var wktReader = new WKTReader(new GeometryFactory(new PrecisionModel(), 4326));

                // Get current season
                var (currentSeason, currentYear) = await GetCurrentSeasonAndYear(cancellationToken);
                
                // Cache farmers by FarmCode
                var uniqueFarmCodes = plotImportRows
                    .Where(p => !string.IsNullOrWhiteSpace(p.FarmCode))
                    .Select(p => p.FarmCode)
                    .Distinct()
                    .ToList();
                    
                var farmers = await farmerRepo.ListAsync(f => uniqueFarmCodes.Contains(f.FarmCode));
                var farmerLookup = farmers.ToDictionary(f => f.FarmCode, f => f);
                
                // Cache rice varieties by name
                var uniqueVarietyNames = plotImportRows
                    .Where(p => !string.IsNullOrWhiteSpace(p.RiceVarietyName))
                    .Select(p => p.RiceVarietyName)
                    .Distinct()
                    .ToList();
                    
                var riceVarieties = await riceVarietyRepo.ListAsync(
                    rv => uniqueVarietyNames.Contains(rv.VarietyName));
                var varietyLookup = riceVarieties.ToDictionary(rv => rv.VarietyName, rv => rv);

                // Dictionary to store validated polygons per row index
                var validatedPolygons = new Dictionary<int, (Polygon boundary, Point coordinate)>();

                // Validate
                var validationErrors = new List<string>();
                for (int i = 0; i < plotImportRows.Count; i++)
                {
                    var row = plotImportRows[i];
                    var rowNumber = i + 2;

                    // Skip rows with no plot data (empty rows from template)
                    if (!row.SoThua.HasValue && !row.SoTo.HasValue && !row.Area.HasValue)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.FarmCode))
                    {
                        validationErrors.Add($"Row {rowNumber}: FarmCode is required");
                        continue;
                    }

                    if (!farmerLookup.ContainsKey(row.FarmCode))
                    {
                        validationErrors.Add(
                            $"Row {rowNumber}: Farmer '{row.FarmCode}' not found. Please import farmers first.");
                        continue;
                    }

                    if (!row.SoThua.HasValue || row.SoThua.Value <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: SoThua is required and must be > 0");
                    }

                    if (!row.SoTo.HasValue || row.SoTo.Value <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: SoTo is required and must be > 0");
                    }

                    if (!row.Area.HasValue || row.Area.Value <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: Area is required and must be > 0");
                    }

                    // Validate rice variety if provided
                    if (!string.IsNullOrWhiteSpace(row.RiceVarietyName))
                    {
                        if (!varietyLookup.ContainsKey(row.RiceVarietyName))
                        {
                            validationErrors.Add(
                                $"Row {rowNumber}: Rice variety '{row.RiceVarietyName}' not found. Check 'Rice_Varieties' sheet.");
                        }

                        if (currentSeason == null)
                        {
                            validationErrors.Add(
                                $"Row {rowNumber}: Cannot create cultivation - no current season found");
                        }
                    }

                    // Validate polygon if provided
                    if (!string.IsNullOrWhiteSpace(row.BoundaryWKT))
                    {
                        try
                        {
                            var boundary = wktReader.Read(row.BoundaryWKT) as Polygon;
                            
                            if (boundary == null)
                            {
                                validationErrors.Add(
                                    $"Row {rowNumber}: Invalid polygon format. Expected WKT POLYGON format.");
                                continue;
                            }

                            if (!boundary.IsValid)
                            {
                                validationErrors.Add(
                                    $"Row {rowNumber}: Polygon is not geometrically valid.");
                                continue;
                            }

                            boundary.SRID = 4326;

                            // Validate area matches (10% tolerance)
                            if (row.Area.HasValue)
                            {
                                var drawnAreaHa = CalculateAreaInHectares(boundary);
                                var registeredArea = row.Area.Value;
                                var differencePercent = Math.Abs((drawnAreaHa - registeredArea) / registeredArea * 100);

                                if (differencePercent > 10)
                                {
                                    validationErrors.Add(
                                        $"Row {rowNumber}: Polygon area ({Math.Round(drawnAreaHa, 2)} ha) differs by {Math.Round(differencePercent, 2)}% from registered area ({Math.Round(registeredArea, 2)} ha). Maximum allowed difference is 10%.");
                                    continue;
                                }
                            }

                            // Parse coordinate or use centroid
                            Point coordinate;
                            if (!string.IsNullOrWhiteSpace(row.CoordinateWKT))
                            {
                                coordinate = wktReader.Read(row.CoordinateWKT) as Point;
                                if (coordinate == null)
                                {
                                    _logger.LogWarning(
                                        "Row {RowNumber}: Invalid coordinate format. Using centroid instead.", 
                                        rowNumber);
                                    coordinate = boundary.Centroid;
                                }
                                else if (!boundary.Contains(coordinate))
                                {
                                    _logger.LogWarning(
                                        "Row {RowNumber}: Coordinate is outside polygon boundary. Using centroid instead.", 
                                        rowNumber);
                                    coordinate = boundary.Centroid;
                                }
                                else
                                {
                                    coordinate.SRID = 4326;
                                }
                            }
                            else
                            {
                                coordinate = boundary.Centroid;
                                coordinate.SRID = 4326;
                            }

                            // Store validated polygon for this row
                            validatedPolygons[i] = (boundary, coordinate);
                        }
                        catch (Exception ex)
                        {
                            validationErrors.Add(
                                $"Row {rowNumber}: Error parsing polygon - {ex.Message}");
                        }
                    }
                }

                if (validationErrors.Any())
                {
                    return Result<List<PlotResponse>>.Failure(
                        $"Validation failed:\n{string.Join("\n", validationErrors)}");
                }

                // Process plots
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                var plotList = new List<Plot>();
                var plotCultivationsToCreate = new List<PlotCultivation>();
                var cultivationVersionsToCreate = new List<CultivationVersion>();
                var plotsNeedingPolygonTasks = new List<Plot>(); // Track plots that need supervisor drawing
                var skippedRows = 0;
                var plotsWithImportedPolygons = 0; // Track imported polygons

                foreach (var row in plotImportRows)
                {
                    // Skip empty rows
                    if (!row.SoThua.HasValue && !row.SoTo.HasValue && !row.Area.HasValue)
                    {
                        skippedRows++;
                        continue;
                    }

                    if (!farmerLookup.TryGetValue(row.FarmCode, out var farmer))
                    {
                        continue;
                    }

                    // Check for duplicates
                    var existingPlot = await plotRepo.FindAsync(p => 
                        p.SoThua == row.SoThua && 
                        p.SoTo == row.SoTo && 
                        p.FarmerId == farmer.Id);
                        
                    if (existingPlot != null)
                    {
                        _logger.LogWarning(
                            "Skipping duplicate plot: {FarmCode} Plot#{PlotNumber} (SoThua:{SoThua}, SoTo:{SoTo})",
                            row.FarmCode, row.PlotNumber, row.SoThua, row.SoTo);
                        continue;
                    }

                    var plotId = await plotRepo.GenerateNewGuid(Guid.NewGuid());
                    
                    // Determine boundary and status based on whether polygon was imported
                    Polygon boundary;
                    Point? coordinate = null;
                    PlotStatus status;
                    var rowIndex = plotImportRows.IndexOf(row);
                    
                    if (validatedPolygons.TryGetValue(rowIndex, out var polygonData))
                    {
                        // Use imported polygon
                        boundary = polygonData.boundary;
                        coordinate = polygonData.coordinate;
                        status = PlotStatus.Active; // Plot is complete with polygon
                        plotsWithImportedPolygons++;
                        
                        _logger.LogInformation(
                            "Plot {FarmCode} #{PlotNumber} (SoThua:{SoThua}, SoTo:{SoTo}) imported with polygon - Status: Active",
                            row.FarmCode, row.PlotNumber, row.SoThua, row.SoTo);
                    }
                    else
                    {
                        // Use dummy boundary - will need supervisor to draw
                        var coordinates = new[]
                        {
                            new Coordinate(0, 0),
                            new Coordinate(0, 0.001),
                            new Coordinate(0.001, 0.001),
                            new Coordinate(0.001, 0),
                            new Coordinate(0, 0)
                        };
                        boundary = geometryFactory.CreatePolygon(coordinates);
                        status = PlotStatus.PendingPolygon; // Needs polygon drawing
                    }

                    var newPlot = new Plot
                    {
                        Id = plotId,
                        SoThua = row.SoThua,
                        SoTo = row.SoTo,
                        Area = row.Area.Value,
                        FarmerId = farmer.Id,
                        SoilType = row.SoilType,
                        Status = status,
                        Boundary = boundary,
                        Coordinate = coordinate
                    };

                    plotList.Add(newPlot);
                    
                    // Only add to polygon task list if no polygon was imported
                    if (status == PlotStatus.PendingPolygon)
                    {
                        plotsNeedingPolygonTasks.Add(newPlot);
                    }

                    // Create PlotCultivation if rice variety specified
                    if (!string.IsNullOrWhiteSpace(row.RiceVarietyName) && 
                        varietyLookup.TryGetValue(row.RiceVarietyName, out var riceVariety) &&
                        currentSeason != null)
                    {
                        var plotCultivationId = Guid.NewGuid();
                        var plotCultivation = new PlotCultivation
                        {
                            Id = plotCultivationId,
                            PlotId = plotId,
                            SeasonId = currentSeason.Id,
                            RiceVarietyId = riceVariety.Id,
                            PlantingDate = DateTime.UtcNow,
                            Area = row.Area.Value,
                            Status = CultivationStatus.Planned
                        };

                        plotCultivationsToCreate.Add(plotCultivation);

                        // Create first version for this PlotCultivation
                        var firstVersion = new CultivationVersion
                        {
                            PlotCultivationId = plotCultivationId,
                            VersionName = "Initial Version",
                            VersionOrder = 1,
                            IsActive = true,
                            Reason = "Created during plot import",
                            ActivatedAt = DateTime.UtcNow
                        };

                        cultivationVersionsToCreate.Add(firstVersion);
                    }
                }

                // Save everything
                if (plotList.Any())
                {
                    await plotRepo.AddRangeAsync(plotList);
                }

                if (plotCultivationsToCreate.Any())
                {
                    var plotCultivationRepo = _unitOfWork.Repository<PlotCultivation>();
                    await plotCultivationRepo.AddRangeAsync(plotCultivationsToCreate);
                    
                    _logger.LogInformation(
                        "Creating {Count} PlotCultivation records for season {SeasonName} {Year}",
                        plotCultivationsToCreate.Count,
                        currentSeason?.SeasonName,
                        currentYear);
                }

                if (cultivationVersionsToCreate.Any())
                {
                    var cultivationVersionRepo = _unitOfWork.Repository<CultivationVersion>();
                    await cultivationVersionRepo.AddRangeAsync(cultivationVersionsToCreate);
                    
                    _logger.LogInformation(
                        "Creating {Count} CultivationVersion records (initial versions)",
                        cultivationVersionsToCreate.Count);
                }

                await _unitOfWork.CompleteAsync();

                // Create response
                var plotCreateSuccessList = new List<PlotResponse>();
                foreach (var plot in plotList)
                {
                    var farmer = await farmerRepo.GetFarmerByIdAsync(plot.FarmerId, cancellationToken);
                    plotCreateSuccessList.Add(new PlotResponse
                    {
                        PlotId = plot.Id,
                        SoThua = plot.SoThua,
                        SoTo = plot.SoTo,
                        Area = plot.Area,
                        FarmerId = plot.FarmerId,
                        FarmerName = farmer?.FullName ?? string.Empty,
                        SoilType = plot.SoilType,
                        Status = plot.Status,
                        GroupId = plot.GroupPlots.FirstOrDefault()?.GroupId
                    });
                }

                // Publish event for polygon assignment
                if (plotsNeedingPolygonTasks.Any())
                {
                    // Only create responses for plots that need polygon drawing
                    var plotsNeedingTasksResponse = plotCreateSuccessList
                        .Where(pr => plotsNeedingPolygonTasks.Any(p => p.Id == pr.PlotId))
                        .ToList();
                        
                    await _mediator.Publish(new PlotImportedEvent
                    {
                        ImportedPlots = plotsNeedingTasksResponse,
                        ClusterManagerId = request.ClusterManagerId,
                        ImportedAt = DateTime.UtcNow,
                        TotalPlotsImported = plotsNeedingTasksResponse.Count
                    }, cancellationToken);
                    
                    _logger.LogInformation(
                        "Published PlotImportedEvent for {PlotCount} plots needing polygon drawing (out of {TotalPlots} total plots imported)",
                        plotsNeedingTasksResponse.Count,
                        plotCreateSuccessList.Count);
                }
                else
                {
                    _logger.LogInformation(
                        "All {PlotCount} imported plots have polygons - no polygon drawing tasks created",
                        plotCreateSuccessList.Count);
                }

                var message = $"Successfully imported {plotCreateSuccessList.Count} plots";
                if (plotsWithImportedPolygons > 0)
                {
                    message += $" ({plotsWithImportedPolygons} with polygons, {plotsNeedingPolygonTasks.Count} pending polygon drawing)";
                }
                if (plotCultivationsToCreate.Any())
                {
                    message += $" with {plotCultivationsToCreate.Count} cultivation records";
                    if (cultivationVersionsToCreate.Any())
                    {
                        message += $" and {cultivationVersionsToCreate.Count} initial versions";
                    }
                    message += $" for {currentSeason?.SeasonName} {currentYear}";
                }
                if (skippedRows > 0)
                {
                    message += $" ({skippedRows} empty rows skipped)";
                }

                return Result<List<PlotResponse>>.Success(plotCreateSuccessList, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing plots");
                return Result<List<PlotResponse>>.Failure(
                    $"Import failed: {ex.Message}");
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
        
        /// <summary>
        /// Calculate area in hectares from a polygon with SRID 4326 (WGS84)
        /// </summary>
        private decimal CalculateAreaInHectares(Polygon polygon)
        {
            // For SRID 4326 (WGS84 lat/lon), approximate conversion
            // This uses a simple approximation - area in square degrees * conversion factor
            var areaInSquareDegrees = polygon.Area;
            
            // Approximate meters per degree at equator (111,319.9 meters per degree)
            var metersPerDegree = 111319.9;
            var areaInSquareMeters = areaInSquareDegrees * metersPerDegree * metersPerDegree;
            
            // Convert to hectares (1 hectare = 10,000 square meters)
            var hectares = (decimal)(areaInSquareMeters / 10000.0);
            
            return Math.Round(hectares, 2);
        }
    }
}
