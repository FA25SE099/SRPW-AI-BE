using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Commands.ImportExcel
{
    public class ImportPlotByExcelCommandHandler : IRequestHandler<ImportPlotByExcelCommand, Result<List<PlotResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericExcel _genericExcel;

        public ImportPlotByExcelCommandHandler(IUnitOfWork unitOfWork, IGenericExcel genericExcel)
        {
            _unitOfWork = unitOfWork;
            _genericExcel = genericExcel;
        }

        public async Task<Result<List<PlotResponse>>> Handle(ImportPlotByExcelCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Change excel file back to list
                var plotListCreateInput = await _genericExcel.ExcelToListT<PlotRequest>(request.ExcelFile);
                if (plotListCreateInput == null || !plotListCreateInput.Any())
                {
                    return Result<List<PlotResponse>>.Failure("The uploaded Excel file is empty or invalid.");
                }

                var plotRepo = _unitOfWork.Repository<Plot>();
                var farmerRepo = _unitOfWork.FarmerRepository; // Use the FarmerRepository from UnitOfWork

                // Validate all plots before processing
                var validationErrors = new List<string>();
                for (int i = 0; i < plotListCreateInput.Count; i++)
                {
                    var plot = plotListCreateInput[i];
                    var rowNumber = i + 2; // Excel row number (accounting for header)

                    if (!plot.SoThua.HasValue || plot.SoThua.Value <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: SoThua is required and must be greater than 0");
                    }

                    if (!plot.SoTo.HasValue || plot.SoTo.Value <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: SoTo is required and must be greater than 0");
                    }

                    if (plot.Area <= 0)
                    {
                        validationErrors.Add($"Row {rowNumber}: Area must be greater than 0");
                    }

                    if (plot.FarmerId == Guid.Empty)
                    {
                        validationErrors.Add($"Row {rowNumber}: FarmerId is required");
                    }

                    if (!string.IsNullOrWhiteSpace(plot.SoilType) && plot.SoilType.Length > 100)
                    {
                        validationErrors.Add($"Row {rowNumber}: SoilType must not exceed 100 characters");
                    }
                    
                }

                if (validationErrors.Any())
                {
                    return Result<List<PlotResponse>>.Failure(
                        $"Excel validation failed:\n{string.Join("\n", validationErrors)}");
                }

                for (int i = 0; i < plotListCreateInput.Count; i++)
                {
                    var plot = plotListCreateInput[i];
                    var rowNumber = i + 2; 

                    var farmerExists = await farmerRepo.ExistAsync(plot.FarmerId, cancellationToken);
                    if (!farmerExists)
                    {
                        return Result<List<PlotResponse>>.Failure(
                            $"Row {rowNumber}: Farmer with ID {plot.FarmerId} does not exist. Please verify the FarmerId in your Excel file.");
                    }

                    var duplicateCount = await plotRepo.FindAsync(p => p.SoThua == plot.SoThua
                                                                      && p.SoTo == plot.SoTo
                                                                      && p.FarmerId == plot.FarmerId);
                    if (duplicateCount != null)
                    {
                        return Result<List<PlotResponse>>.Failure(
                            $"Row {rowNumber}: The uploaded Excel file contains duplicate plot in the system, SoThua: {plot.SoThua}, SoTo: {plot.SoTo}. Please check again!");
                    }
                }
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                var plotList = new List<Plot>();
                var plotCreateSuccessList = new List<PlotResponse>();

                foreach (var plot in plotListCreateInput)
                {
                    var id = await plotRepo.GenerateNewGuid(Guid.NewGuid());
                    var coordinates = new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(0, 0.001),
                        new Coordinate(0.001, 0.001),
                        new Coordinate(0.001, 0),
                        new Coordinate(0, 0) 
                    };
                    var defaultBoundary = geometryFactory.CreatePolygon(coordinates);
                    var newPlot = new Plot
                    {
                        Id = id,
                        SoThua = plot.SoThua,
                        SoTo = plot.SoTo,
                        Area = plot.Area,
                        FarmerId = plot.FarmerId,
                        SoilType = plot.SoilType,
                        Status = Domain.Enums.PlotStatus.Active,
                        Boundary = defaultBoundary 
                    };

                    plotList.Add(newPlot);
                }

                // Add new plot records
                if (plotList.Any())
                {
                    await plotRepo.AddRangeAsync(plotList);
                }

                // Save all changes
                var result = await _unitOfWork.CompleteAsync();
                if (result <= 0)
                {
                    return Result<List<PlotResponse>>.Failure("Failed to import plots.");
                }

                // Create response objects
                foreach (var plot in plotList)
                {
                    var farmer = await farmerRepo.GetFarmerByIdAsync(plot.FarmerId, cancellationToken);

                    var plotResponse = new PlotResponse
                    {
                        PlotId = plot.Id,
                        SoThua = plot.SoThua,
                        SoTo = plot.SoTo,
                        Area = plot.Area,
                        FarmerId = plot.FarmerId,
                        FarmerName = farmer?.FullName ?? string.Empty,
                        SoilType = plot.SoilType,
                        Status = plot.Status,
                        GroupId = plot.GroupId
                    };
                    plotCreateSuccessList.Add(plotResponse);
                }

                return Result<List<PlotResponse>>.Success(
                    plotCreateSuccessList,
                    $"Successfully created {plotCreateSuccessList.Count} plots!");
            }
            catch (Exception ex)
            {
                return Result<List<PlotResponse>>.Failure(
                    $"An error occurred while importing plots: {ex.Message}");
            }
        }
    }
}
