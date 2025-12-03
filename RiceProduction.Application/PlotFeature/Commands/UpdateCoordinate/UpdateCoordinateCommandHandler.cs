using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Commands.UpdateCoordinate
{
    public class UpdateCoordinateCommandHandler : IRequestHandler<UpdateCoordinateCommand, Result<bool>>
    {
        IUnitOfWork _unitOfWork;
        ILogger<UpdateCoordinateCommandHandler> _logger;

        public UpdateCoordinateCommandHandler(IUnitOfWork unitOfWork, ILogger<UpdateCoordinateCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public async Task<Result<bool>> Handle(UpdateCoordinateCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var plot = await _unitOfWork.Repository<Plot>().FindAsync(p => p.Id == request.PlotId);
                if (plot == null)
                {
                    return Result<bool>.Failure("Plot not found");
                }
                NetTopologySuite.Geometries.Point? point;
                try
                {
                    var geoJsonReader = new GeoJsonReader();
                    var geometry = geoJsonReader.Read<Geometry>(request.CoordinateGeoJson);
                    point = geometry as NetTopologySuite.Geometries.Point;
                    if (point == null)
                    {
                        return Result<bool>.Failure("Invalid coordinate geometry. Must be a point type");
                    }
                    if (point.X < -180 || point.X > 180 || point.Y < -90 || point.Y > 90)
                    {
                        return Result<bool>.Failure("Invalid coordinates. Longitude must be between -180 and 180, Latitude between -90 and 90.");
                    }
                    point.SRID = 4326;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing coordinate GeoJSON for plot {PlotId}", request.PlotId);
                    return Result<bool>.Failure($"Invalid GeoJSON format: {ex.Message}");
                }
                plot.Coordinate = point;
                _unitOfWork.Repository<Plot>().Update(plot);
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation(
               "Updated coordinate (marker point) for Plot {PlotId} to [{Longitude}, {Latitude}]. Boundary unchanged.",
               request.PlotId, point.X, point.Y);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating coordinate for plot {PlotId}", request.PlotId);
                return Result<bool>.Failure($"Error updating coordinate: {ex.Message}");
            }
        }
    }
}
