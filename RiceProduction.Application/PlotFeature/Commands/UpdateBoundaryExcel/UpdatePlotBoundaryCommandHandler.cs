using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Commands.UpdateBoundaryExcel
{
    public class UpdatePlotBoundaryCommandHandler : IRequestHandler<UpdatePlotBoundaryCommand, Result<List<BoundaryResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;
        
        public UpdatePlotBoundaryCommandHandler(IUnitOfWork unitOfWork, IGenericExcel genericExcel)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
        }
        public async Task<Result<List<BoundaryResponse>>> Handle(UpdatePlotBoundaryCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var boundaryInputList = await _genericExcel.ExcelToListT<BoundaryUpdateRequest>(request.ExcelFile);
                if (boundaryInputList == null || !boundaryInputList.Any())
                {
                    return Result<List<BoundaryResponse>>.Failure("The upload Excel file is empty or invalid");
                }
                var plotRepo = _unitOfWork.Repository<Plot>();
                var farmerRepo = _unitOfWork.FarmerRepository;
                var validatorErrors = new List<string>();
                var successfulUpdates = new List<BoundaryResponse>();
                var wktReader = new WKTReader();
               

                for (var i = 0; i < boundaryInputList.Count; i++)
                {
                    var boundaryInput = boundaryInputList[i];
                    var rowNumber = i + 2;
                    try
                    {
                        if (boundaryInput.FarmerId == Guid.Empty)
                        {
                            validatorErrors.Add($"Row {rowNumber}: FarmerId is required");
                        }
                        if (!boundaryInput.SoThua.HasValue || boundaryInput.SoThua.Value <= 0)
                        {
                            validatorErrors.Add($"Row {rowNumber}: SoThua is required and must be greater than 0");
                            continue;
                        }
                        if (!boundaryInput.SoTo.HasValue || boundaryInput.SoTo.Value <= 0)
                        {
                            validatorErrors.Add($"Row {rowNumber}: SoTo is required and must be greater than 0");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(boundaryInput.Boundary))
                        {
                            continue; // Skip this row silently - it's OK to have empty boundaries
                        }
                        var farmerExist = await farmerRepo.ExistAsync(boundaryInput.FarmerId, cancellationToken);
                        if (!farmerExist)
                        {
                            validatorErrors.Add($"Row {rowNumber}: Farmer Id {boundaryInput.FarmerId} does not exist");
                            continue;
                        }
                        var existingPlot = await plotRepo.FindAsync
                            (p => p.SoThua == boundaryInput.SoThua
                        && p.SoTo == boundaryInput.SoTo
                        && p.FarmerId == boundaryInput.FarmerId);

                        if (existingPlot != null)
                        {
                            validatorErrors.Add($"Row {rowNumber}: Plot does not exist");
                            continue;
                        }
                        var boundary = wktReader.Read(boundaryInput.Boundary) as Polygon;
                        if (boundary == null)
                        {
                            validatorErrors.Add($"Row {rowNumber}: Invalid Boundary format. Expected WKT Polygon");
                            continue;
                        }
                        boundary.SRID = 4326;

                        Point coordinate;
                        if (!string.IsNullOrEmpty(boundaryInput.Coordinate))
                        {
                            coordinate = wktReader.Read(boundaryInput.Coordinate) as Point;
                            if (coordinate == null)
                            {
                                validatorErrors.Add($"Row {rowNumber}: Invalid Coordinate format. Expected WKT Point");
                                continue;
                            }
                            coordinate.SRID = 4326;

                            if (!boundary.Contains(coordinate))
                            {
                                validatorErrors.Add($"Row {rowNumber}: Coordinate must be within the boundary polygon. Using centroid instead.");
                                coordinate = boundary.Centroid as Point;
                            }
                        }
                        else
                        {
                            coordinate = boundary.Centroid as Point;
                            coordinate.SRID = 4326;
                        }
                        existingPlot.Area = CalculationArea(boundary);
                        plotRepo.Update(existingPlot);
                        var farmer = await farmerRepo.GetFarmerByIdAsync(existingPlot.FarmerId, cancellationToken);
                        successfulUpdates.Add(new BoundaryResponse
                        {
                            PlotId = existingPlot.Id,
                            Boundary = boundary.ToText(),
                            Coordinate = coordinate.ToText(),
                        });

                    }
                    catch (Exception ex)
                    {
                        validatorErrors.Add($"Row {rowNumber}: Error - {ex.Message}");
                        continue;
                    }
                    
                }
                if (successfulUpdates.Any())
                {
                    await _unitOfWork.CompleteAsync();
                }
                if (validatorErrors.Any())
                {
                    var errorMessage = string.Join("; ", validatorErrors);

                    if (successfulUpdates.Any())
                    {
                        return Result<List<BoundaryResponse>>.Success(
                            successfulUpdates,
                            $"Partially completed: {successfulUpdates.Count} updated, {validatorErrors.Count} errors. Errors: {errorMessage}"
                        );
                    }

                    return Result<List<BoundaryResponse>>.Failure(errorMessage);
                }
                return Result<List<BoundaryResponse>>.Success(
                    successfulUpdates,
                    $"Successfully updated {successfulUpdates.Count} plot boundaries"
                );

            }
            catch (Exception ex)
            {
                return Result<List<BoundaryResponse>>.Failure($"An error occurred: {ex.Message}");
            }

        }
        private decimal CalculationArea(Polygon polygon)
        {
            var area = polygon.Area;
            var areaInSquareMeters = area * 111319.9 * 111319.9;
            return Math.Round((decimal)areaInSquareMeters, 2);
        }
    }
}
