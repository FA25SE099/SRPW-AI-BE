using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotMaterialResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanPlotMaterials;

public class GetPlanPlotMaterialsQueryHandler : IRequestHandler<GetPlanPlotMaterialsQuery, Result<PlanPlotMaterialsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlanPlotMaterialsQueryHandler> _logger;

    public GetPlanPlotMaterialsQueryHandler(IUnitOfWork unitOfWork, ILogger<GetPlanPlotMaterialsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PlanPlotMaterialsResponse>> Handle(GetPlanPlotMaterialsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(
                match: p => p.Id == request.PlanId,
                includeProperties: q => q
                    .Include(p => p.Group).ThenInclude(g => g!.Plots)
                        .ThenInclude(plot => plot.Farmer)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks)
                            .ThenInclude(t => t.ProductionPlanTaskMaterials)
                                .ThenInclude(m => m.Material)
                                    .ThenInclude(mat => mat.MaterialPrices)
            );

            if (plan == null)
            {
                return Result<PlanPlotMaterialsResponse>.Failure($"Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            if (plan.Group?.Plots == null || !plan.Group.Plots.Any())
            {
                return Result<PlanPlotMaterialsResponse>.Failure("No plots found for this plan.", "NoPlotsFound");
            }

            var currentDate = DateTime.UtcNow;
            var plotMaterialDetails = new List<PlotMaterialDetailResponse>();

            foreach (var plot in plan.Group.Plots)
            {
                var materialDictionary = new Dictionary<Guid, PlotMaterialItemResponse>();

                foreach (var stage in plan.CurrentProductionStages)
                {
                    foreach (var task in stage.ProductionPlanTasks)
                    {
                        foreach (var taskMaterial in task.ProductionPlanTaskMaterials)
                        {
                            var material = taskMaterial.Material;
                            var currentPrice = material.MaterialPrices
                                .Where(p => p.ValidFrom <= currentDate)
                                .OrderByDescending(p => p.ValidFrom)
                                .FirstOrDefault();

                            if (currentPrice == null)
                            {
                                _logger.LogWarning("No price found for material {MaterialId} '{MaterialName}'", 
                                    material.Id, material.Name);
                                continue;
                            }

                            var isOutdated = currentPrice.ValidTo.HasValue && currentPrice.ValidTo.Value < currentDate;
                            var totalQuantity = taskMaterial.QuantityPerHa * plot.Area;
                            var totalCost = totalQuantity * currentPrice.PricePerMaterial;

                            if (materialDictionary.ContainsKey(material.Id))
                            {
                                var existingMaterial = materialDictionary[material.Id];
                                existingMaterial.TotalQuantity += totalQuantity;
                                existingMaterial.TotalCost += totalCost;
                            }
                            else
                            {
                                materialDictionary[material.Id] = new PlotMaterialItemResponse
                                {
                                    MaterialId = material.Id,
                                    MaterialName = material.Name,
                                    MaterialUnit = material.Unit,
                                    ImgUrl = null,
                                    QuantityPerHa = taskMaterial.QuantityPerHa,
                                    TotalQuantity = totalQuantity,
                                    PricePerUnit = currentPrice.PricePerMaterial,
                                    PriceValidFrom = currentPrice.ValidFrom,
                                    PriceValidTo = currentPrice.ValidTo,
                                    IsOutdated = isOutdated,
                                    TotalCost = totalCost
                                };
                            }
                        }
                    }
                }

                var plotMaterialDetail = new PlotMaterialDetailResponse
                {
                    PlotId = plot.Id,
                    PlotArea = plot.Area,
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    FarmerName = plot.Farmer?.FullName ?? "Unknown",
                    Materials = materialDictionary.Values.OrderBy(m => m.MaterialName).ToList(),
                    TotalEstimatedCost = materialDictionary.Values.Sum(m => m.TotalCost)
                };

                plotMaterialDetails.Add(plotMaterialDetail);
            }

            var response = new PlanPlotMaterialsResponse
            {
                PlanId = plan.Id,
                PlanName = plan.PlanName,
                Plots = plotMaterialDetails.OrderBy(p => p.SoThua).ThenBy(p => p.SoTo).ToList()
            };

            return Result<PlanPlotMaterialsResponse>.Success(response, "Successfully retrieved plot materials with prices.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plot materials for plan {PlanId}", request.PlanId);
            return Result<PlanPlotMaterialsResponse>.Failure("Failed to retrieve plot materials.", "GetPlotMaterialsFailed");
        }
    }
}

