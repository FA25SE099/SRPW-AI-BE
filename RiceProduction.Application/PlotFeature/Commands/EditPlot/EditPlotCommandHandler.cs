using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using RiceProduction.Domain.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RiceProduction.Application.PlotFeature.Commands.EditPlot
{
    public class EditPlotCommandHandler : IRequestHandler<EditPlotCommand, Result<UpdatePlotRequest>>
    {
        private readonly IApplicationDbContext _context;
        private readonly WKTReader _wktReader;
        private readonly ILogger<EditPlotCommandHandler> _logger;

        public EditPlotCommandHandler(IApplicationDbContext context, ILogger<EditPlotCommandHandler> logger)
        {
            _context = context;
            _wktReader = new WKTReader(new GeometryFactory(new PrecisionModel(), PlotGeometryHelper.Wgs84Srid));
            _logger = logger;
        }

        public async Task<Result<UpdatePlotRequest>> Handle(EditPlotCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var plot = await _context.Plots
                    .FirstOrDefaultAsync(p => p.Id == request.Request.PlotId, cancellationToken);
                if (plot == null)
                {
                    return Result<UpdatePlotRequest>.Failure("Plot not found");
                }

                Polygon? polygonBoundary = null;
                if (!string.IsNullOrWhiteSpace(request.Request.Boundary))
                {
                    var editabilityCheck = await CheckPlotPolygonEditabilityAsync(plot, cancellationToken);
                    if (!editabilityCheck.isEditable)
                    {
                        return Result<UpdatePlotRequest>.Failure(editabilityCheck.reason);
                    }

                    if (!TryParseGeometry(request.Request.Boundary, "boundary", "Polygon", out polygonBoundary, out var boundaryError))
                    {
                        return Result<UpdatePlotRequest>.Failure(boundaryError!);
                    }
                }

                Point? coordinatePoint = null;
                if (!string.IsNullOrWhiteSpace(request.Request.Coordinate) &&
                    !TryParseGeometry(request.Request.Coordinate, "coordinate", "Point", out coordinatePoint, out var coordinateError))
                {
                    return Result<UpdatePlotRequest>.Failure(coordinateError!);
                }

                plot.FarmerId = request.Request.FarmerId;

                // GroupId assignment is handled through GroupPlot many-to-many; null GroupId leaves existing memberships intact
                if (request.Request.GroupId.HasValue)
                {
                    var groupError = await UpdatePlotGroupAssignmentAsync(plot, request.Request.GroupId.Value, cancellationToken);
                    if (groupError != null)
                    {
                        return Result<UpdatePlotRequest>.Failure(groupError);
                    }
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

                _context.Plots.Update(plot);
                await _context.SaveChangesAsync(cancellationToken);

                return Result<UpdatePlotRequest>.Success(request.Request);
            }
            catch (Exception ex)
            {
                return Result<UpdatePlotRequest>.Failure($"An error occurred while updating plot: {ex.Message}");
            }
        }

        private bool TryParseGeometry<TGeometry>(
            string wkt,
            string fieldName,
            string expectedTypeName,
            out TGeometry? geometry,
            out string? error)
            where TGeometry : Geometry
        {
            geometry = null;
            error = null;

            try
            {
                geometry = _wktReader.Read(wkt) as TGeometry;
                if (geometry == null)
                {
                    error = $"Invalid {fieldName} format. Expected a {expectedTypeName}.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Error parsing {fieldName}: {ex.Message}";
                return false;
            }
        }

        private async Task<string?> UpdatePlotGroupAssignmentAsync(Plot plot, Guid groupId, CancellationToken cancellationToken)
        {
            var targetGroup = await _context.Groups
                .AsNoTracking()
                .Include(g => g.YearSeason)
                .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

            if (targetGroup == null)
            {
                return $"Group with ID {groupId} not found";
            }

            // A plot can belong to multiple groups across seasons, but only one group per season
            if (targetGroup.YearSeason?.SeasonId != null)
            {
                var targetSeasonId = targetGroup.YearSeason.SeasonId;
                var existingGroupPlots = await _context.GroupPlots
                    .Include(gp => gp.Group)
                        .ThenInclude(g => g.YearSeason)
                    .Where(gp => gp.PlotId == plot.Id &&
                                 gp.GroupId != groupId &&
                                 gp.Group.YearSeason != null &&
                                 gp.Group.YearSeason.SeasonId == targetSeasonId)
                    .ToListAsync(cancellationToken);

                if (existingGroupPlots.Any())
                {
                    _context.GroupPlots.RemoveRange(existingGroupPlots);
                }
            }

            var alreadyInGroup = await _context.GroupPlots
                .AsNoTracking()
                .AnyAsync(gp => gp.PlotId == plot.Id && gp.GroupId == groupId, cancellationToken);
            if (!alreadyInGroup)
            {
                _context.GroupPlots.Add(new GroupPlot
                {
                    GroupId = groupId,
                    PlotId = plot.Id
                });
            }

            return null;
        }

        private async Task<(bool isEditable, string reason)> CheckPlotPolygonEditabilityAsync(Plot plot, CancellationToken cancellationToken)
        {
            try
            {
                var farmerClusterId = await _context.Farmers
                    .AsNoTracking()
                    .Where(f => f.Id == plot.FarmerId)
                    .Select(f => f.ClusterId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!farmerClusterId.HasValue)
                {
                    return (true, "Farmer not assigned to cluster");
                }

                var allSeasons = await _context.Seasons
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                var resolvedSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, DateTime.UtcNow);
                if (resolvedSeason == null)
                {
                    return (true, "No active season");
                }

                var (currentSeason, currentYear) = resolvedSeason.Value;

                var yearSeason = await _context.YearSeasons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ys => ys.ClusterId == farmerClusterId.Value
                                            && ys.SeasonId == currentSeason.Id
                                            && ys.Year == currentYear,
                                         cancellationToken);

                if (yearSeason == null)
                {
                    return (true, "No active year-season for cluster");
                }

                var isInGroup = await _context.GroupPlots
                    .AsNoTracking()
                    .AnyAsync(gp => gp.PlotId == plot.Id && gp.Group.YearSeasonId == yearSeason.Id, cancellationToken);

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
    }
}
