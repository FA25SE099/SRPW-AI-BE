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
        private readonly IUnitOfWork _unitOfWork;
        private readonly WKTReader _wktReader;
        private readonly ILogger<EditPlotCommandHandler> _logger;

        public EditPlotCommandHandler(IUnitOfWork unitOfWork, ILogger<EditPlotCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _wktReader = new WKTReader(new GeometryFactory(new PrecisionModel(), PlotGeometryHelper.Wgs84Srid));
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

                plotRepo.Update(plot);
                await _unitOfWork.CompleteAsync();

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
            var targetGroup = await _unitOfWork.Repository<Group>()
                .FindAsync(g => g.Id == groupId);

            if (targetGroup == null)
            {
                return $"Group with ID {groupId} not found";
            }

            // A plot can belong to multiple groups across seasons, but only one group per season
            if (targetGroup.YearSeason?.SeasonId != null)
            {
                var existingGroupPlots = await _unitOfWork.Repository<GroupPlot>()
                    .GetQueryable()
                    .Include(gp => gp.Group)
                        .ThenInclude(g => g.YearSeason)
                    .Where(gp => gp.PlotId == plot.Id && gp.Group.YearSeason != null && gp.Group.YearSeason.SeasonId == targetGroup.YearSeason.SeasonId)
                    .ToListAsync(cancellationToken);

                foreach (var gp in existingGroupPlots)
                {
                    _unitOfWork.Repository<GroupPlot>().Delete(gp);
                }
            }

            var alreadyInGroup = await _unitOfWork.PlotRepository.IsPlotAssignedToGroupAsync(plot.Id, groupId, cancellationToken);
            if (!alreadyInGroup)
            {
                await _unitOfWork.Repository<GroupPlot>().AddAsync(new GroupPlot
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
                var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(plot.FarmerId, cancellationToken);
                if (farmer == null || !farmer.ClusterId.HasValue)
                {
                    return (true, "Farmer not assigned to cluster");
                }

                var allSeasons = await _unitOfWork.Repository<Season>().ListAllAsync();
                var resolvedSeason = SeasonDateHelper.ResolveCurrentSeason(allSeasons, DateTime.Now);
                if (resolvedSeason == null)
                {
                    return (true, "No active season");
                }

                var (currentSeason, currentYear) = resolvedSeason.Value;

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
    }
}
