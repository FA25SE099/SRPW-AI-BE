using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByArea;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByPlotId;

public class CalculateMaterialsCostByPlotIdQueryHandler : IRequestHandler<CalculateMaterialsCostByPlotIdQuery, Result<CalculateMaterialsCostByAreaResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateMaterialsCostByPlotIdQueryHandler> _logger;

    public CalculateMaterialsCostByPlotIdQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateMaterialsCostByPlotIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateMaterialsCostByAreaResponse>> Handle(CalculateMaterialsCostByPlotIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Retrieve the plot to get its area
            var plot = await _unitOfWork.Repository<Plot>().FindAsync(
                match: p => p.Id == request.PlotId
            );

            if (plot == null)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure($"Plot with ID {request.PlotId} not found.", "PlotNotFound");
            }

            if (plot.Area <= 0)
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("Plot area is not valid or is zero.", "InvalidPlotArea");
            }

            var currentDate = DateTime.UtcNow;
            var priceWarnings = new List<string>();

            // Group materials by MaterialId and sum their quantities
            var groupedMaterials = request.Materials
                .GroupBy(m => m.MaterialId)
                .Select(g => new MaterialQuantityInput
                {
                    MaterialId = g.Key,
                    QuantityPerHa = g.Sum(m => m.QuantityPerHa)
                })
                .ToList();

            // Get unique material IDs
            var materialIds = groupedMaterials.Select(m => m.MaterialId).ToList();

            // Load material details
            var materials = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => materialIds.Contains(m.Id) && m.IsActive
            );

            if (!materials.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No active materials found.", "MaterialsNotFound");
            }

            var materialsDict = materials.ToDictionary(m => m.Id, m => m);

            // Load current prices for materials
            var allPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom <= currentDate
            );

            // Get the most recent valid price for each material
            var currentPrices = allPrices
                .Where(p => !p.ValidTo.HasValue || p.ValidTo.Value.Date >= currentDate.Date)
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p);

            // Calculate costs for each material
            var materialCostItems = new List<MaterialCostItem>();
            decimal totalCostForArea = 0M;

            foreach (var inputMaterial in groupedMaterials)
            {
                if (!materialsDict.TryGetValue(inputMaterial.MaterialId, out var material))
                {
                    priceWarnings.Add($"Material ID {inputMaterial.MaterialId} not found or is inactive.");
                    continue;
                }

                if (!currentPrices.TryGetValue(inputMaterial.MaterialId, out var priceInfo))
                {
                    priceWarnings.Add($"No valid price found for material '{material.Name}' (ID: {material.Id}).");
                    continue;
                }

                var amountPerMaterial = material.AmmountPerMaterial.GetValueOrDefault(1M);
                if (amountPerMaterial <= 0) amountPerMaterial = 1M;

                // Calculate total quantity needed for the plot area
                var totalQuantityNeeded = inputMaterial.QuantityPerHa * plot.Area;

                // Calculate packages needed (ceiling)
                var packagesNeeded = material.IsPartition
                    ? totalQuantityNeeded / amountPerMaterial
                    : Math.Ceiling(totalQuantityNeeded / amountPerMaterial);

                // Calculate actual quantity after ceiling
                var actualQuantity = packagesNeeded * amountPerMaterial;

                // Calculate total cost
                var totalCost = packagesNeeded * priceInfo.PricePerMaterial;

                // Calculate cost per hectare
                var costPerHa = totalCost / plot.Area;

                materialCostItems.Add(new MaterialCostItem
                {
                    MaterialId = material.Id,
                    MaterialName = material.Name,
                    Unit = material.Unit,
                    QuantityPerHa = inputMaterial.QuantityPerHa,
                    TotalQuantityNeeded = totalQuantityNeeded,
                    AmountPerMaterial = amountPerMaterial,
                    PackagesNeeded = packagesNeeded,
                    ActualQuantity = actualQuantity,
                    PricePerMaterial = priceInfo.PricePerMaterial,
                    TotalCost = totalCost,
                    CostPerHa = costPerHa,
                    PriceValidFrom = priceInfo.ValidFrom
                });

                totalCostForArea += totalCost;
            }

            if (!materialCostItems.Any())
            {
                return Result<CalculateMaterialsCostByAreaResponse>.Failure("No valid material cost calculations could be performed.", "NoValidCalculations");
            }

            // Calculate total cost per hectare
            var totalCostPerHa = totalCostForArea / plot.Area;

            var response = new CalculateMaterialsCostByAreaResponse
            {
                Area = plot.Area,
                TotalCostPerHa = totalCostPerHa,
                TotalCostForArea = totalCostForArea,
                MaterialCostItems = materialCostItems,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any()
                ? $"Successfully calculated material costs for Plot ID {request.PlotId} with {priceWarnings.Count} warning(s)."
                : $"Successfully calculated material costs for Plot ID {request.PlotId}.";

            _logger.LogInformation("Calculated material costs for Plot ID {PlotId} (Area: {Area}ha). Total cost: {TotalCost}. Materials: {Count}",
                request.PlotId, plot.Area, totalCostForArea, materialCostItems.Count);

            return Result<CalculateMaterialsCostByAreaResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating material costs for Plot ID {PlotId}", request.PlotId);
            return Result<CalculateMaterialsCostByAreaResponse>.Failure("An error occurred during cost calculation.", "CostCalculationFailed");
        }
    }
}
