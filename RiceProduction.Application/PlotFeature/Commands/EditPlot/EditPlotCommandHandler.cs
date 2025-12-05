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

namespace RiceProduction.Application.PlotFeature.Commands.EditPlot
{
    public class EditPlotCommandHandler : IRequestHandler<EditPlotCommand, Result<UpdatePlotRequest>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly GeometryFactory _geometryFactory;
        private readonly WKTReader _wktReader;

        public EditPlotCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            _wktReader = new WKTReader(_geometryFactory);
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
    }
}
