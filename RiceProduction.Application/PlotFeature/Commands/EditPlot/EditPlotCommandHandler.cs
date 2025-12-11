using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.PlotFeature.Queries;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RiceProduction.Application.PlotFeature.Commands.EditPlot
{
    public class EditPlotCommandHandler : IRequestHandler<EditPlotCommand, Result<UpdatePlotRequest>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly GeometryFactory _geometryFactory;
        private readonly WKTReader _wktReader;
        private readonly ILogger<EditPlotCommandHandler> _logger;

        public EditPlotCommandHandler(IUnitOfWork unitOfWork, ILogger<EditPlotCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            _wktReader = new WKTReader(_geometryFactory);
            _logger = logger;
        }

        public async Task<Result<UpdatePlotRequest>> Handle(EditPlotCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var plotRepo = _unitOfWork.Repository<Plot>();
                var plot = await plotRepo.FindAsync(p => p.Id == request.Request.PlotId);
                if (plot == null)
                {
                    return Result<UpdatePlotRequest>.Failure("Plot not found");
                }

                // Parse Boundary string to Polygon
                Polygon? polygonBoundary = null;
                if (!string.IsNullOrWhiteSpace(request.Request.Boundary))
                {
                    var editabilityCheck = await CheckPlotPolygonEditabilityAsync(plot, cancellationToken);
                    if (!editabilityCheck.isEditable)
                    {
                        return Result<UpdatePlotRequest>.Failure(editabilityCheck.reason);
                    }

                    try
                    {
                        var geometry = _wktReader.Read(request.Request.Boundary);
                        polygonBoundary = geometry as Polygon;

                        if (polygonBoundary == null)
                        {
                            return Result<UpdatePlotRequest>.Failure("Invalid boundary format. Expected a Polygon.");
                        }
                    }
                    catch (Exception ex)
                    {
                        return Result<UpdatePlotRequest>.Failure($"Error parsing boundary: {ex.Message}");
                    }
                }

                // Parse Coordinate string to Point
                Point? coordinatePoint = null;
                if (!string.IsNullOrWhiteSpace(request.Request.Coordinate))
                {
                    try
                    {
                        var geometry = _wktReader.Read(request.Request.Coordinate);
                        coordinatePoint = geometry as Point;

                        if (coordinatePoint == null)
                        {
                            return Result<UpdatePlotRequest>.Failure("Invalid coordinate format. Expected a Point.");
                        }
                    }
                    catch (Exception ex)
                    {
                        return Result<UpdatePlotRequest>.Failure($"Error parsing coordinate: {ex.Message}");
                    }
                }

                // Update plot properties
                plot.FarmerId = request.Request.FarmerId;
                // Note: GroupId assignment is now handled through GroupPlot many-to-many relationship
                // Business rule: A plot can belong to multiple groups, but only one group per season
                if (request.Request.GroupId.HasValue)
                {
                    // Get the target group to find its season
                    var targetGroup = await _unitOfWork.Repository<Group>()
                        .FindAsync(g => g.Id == request.Request.GroupId.Value);
                    
                    if (targetGroup == null)
                    {
                        return Result<UpdatePlotRequest>.Failure($"Group with ID {request.Request.GroupId.Value} not found");
                    }
                    
                    // Only remove GroupPlot associations for the SAME SEASON
                    // This allows the plot to belong to groups in different seasons
                    if (targetGroup.SeasonId.HasValue)
                    {
                        var existingGroupPlots = await _unitOfWork.Repository<GroupPlot>()
                            .GetQueryable()
                            .Include(gp => gp.Group)
                            .Where(gp => gp.PlotId == plot.Id && gp.Group.SeasonId == targetGroup.SeasonId.Value)
                            .ToListAsync(cancellationToken);
                        
                        foreach (var gp in existingGroupPlots)
                        {
                            _unitOfWork.Repository<GroupPlot>().Delete(gp);
                        }
                    }
                    
                    // Check if plot is already in this specific group
                    var alreadyInGroup = await _unitOfWork.PlotRepository.IsPlotAssignedToGroupAsync(plot.Id, request.Request.GroupId.Value, cancellationToken);
                    if (!alreadyInGroup)
                    {
                        // Add new GroupPlot association
                        var newGroupPlot = new GroupPlot
                        {
                            GroupId = request.Request.GroupId.Value,
                            PlotId = plot.Id
                        };
                        await _unitOfWork.Repository<GroupPlot>().AddAsync(newGroupPlot);
                    }
                }
                else
                {
                    // If GroupId is null, we don't remove associations - plot can still belong to other groups
                    // This allows removing a plot from one group without affecting its membership in other groups
                    // If you want to remove ALL associations, that would be a separate operation
                }

                if (polygonBoundary != null)
                {
                    plot.Boundary = polygonBoundary;
                }

                plot.SoThua = request.Request.SoThua;
                plot.SoTo = request.Request.SoTo;
                plot.Area = request.Request.Area;
                plot.SoilType = request.Request.SoilType;

                if (coordinatePoint != null)
                {
                    plot.Coordinate = coordinatePoint;
                }

                plot.Status = request.Request.Status;

                plotRepo.Update(plot);
                await _unitOfWork.CompleteAsync();

                return Result<UpdatePlotRequest>.Success(request.Request);
            }
            catch (Exception ex)
            {
                return Result<UpdatePlotRequest>.Failure($"An error occurred while updating plot: {ex.Message}");
            }
        }

        private async Task<(bool isEditable, string reason)> CheckPlotPolygonEditabilityAsync(Plot plot, CancellationToken cancellationToken)
        {
            try
            {
                var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(plot.FarmerId, cancellationToken);
                if (farmer == null || !farmer.ClusterId.HasValue)
                {
                    return (true, "Farmer not assigned to cluster");
                }

                var (currentSeason, currentYear) = await GetCurrentSeasonAndYearAsync();
                if (currentSeason == null)
                {
                    return (true, "No active season");
                }

                var yearSeasons = await _unitOfWork.Repository<YearSeason>()
                    .ListAsync(ys => ys.ClusterId == farmer.ClusterId.Value 
                              && ys.SeasonId == currentSeason.Id 
                              && ys.Year == currentYear);

                var yearSeason = yearSeasons.FirstOrDefault();
                if (yearSeason == null)
                {
                    return (true, "No active year-season for cluster");
                }

                var isInGroup = await _unitOfWork.PlotRepository
                    .IsPlotAssignedToGroupForYearSeasonAsync(plot.Id, yearSeason.Id, cancellationToken);

                if (isInGroup)
                {
                    return (false, $"Cannot edit plot polygon. This plot is already assigned to a group in the current season ({currentSeason.SeasonName} {currentYear})");
                }

                return (true, "Plot is editable");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking plot editability for PlotId: {PlotId}. Allowing edit.", plot.Id);
                return (true, "Editability check failed, allowing edit");
            }
        }

        private async Task<(Season? season, int year)> GetCurrentSeasonAndYearAsync()
        {
            var today = DateTime.Now;
            var currentMonth = today.Month;
            var currentDay = today.Day;

            var allSeasons = await _unitOfWork.Repository<Season>().ListAllAsync();

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
