using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq;
using System;
using System.Collections.Generic;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;
public class CalculateGroupMaterialCostQueryHandler : IRequestHandler<CalculateGroupMaterialCostQuery, Result<CalculateGroupMaterialCostResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateGroupMaterialCostQueryHandler> _logger;

    public CalculateGroupMaterialCostQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateGroupMaterialCostQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CalculateGroupMaterialCostResponse>> Handle(CalculateGroupMaterialCostQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.UtcNow;

            // --- 1. Tải Group và Plots ---
            var group = await _unitOfWork.Repository<Group>().FindAsync(
                match: g => g.Id == request.GroupId,
                includeProperties: q => q.Include(g => g.GroupPlots).ThenInclude(gp => gp.Plot)
            );

            if (group == null)
            {
                return Result<CalculateGroupMaterialCostResponse>.Failure($"Group with ID {request.GroupId} not found.", "GroupNotFound");
            }
            
            if (group.TotalArea == null || group.TotalArea.Value <= 0)
            {
                return Result<CalculateGroupMaterialCostResponse>.Failure("Group's Total Area is not defined or is zero.", "GroupAreaMissing");
            }
            
            decimal effectiveTotalArea = group.TotalArea.Value;

            // --- 2. Tổng hợp Vật tư Yêu cầu (Quantity/ha) ---
            var aggregatedMaterials = request.Materials
                .GroupBy(m => m.MaterialId)
                .Select(g => new MaterialInputModel
                {
                    MaterialId = g.Key,
                    // Giữ nguyên Quantity/ha (chỉ lấy lần xuất hiện đầu tiên, hoặc max nếu logic yêu cầu)
                    Quantity = g.First().Quantity 
                })
                .ToList();

            var materialIds = aggregatedMaterials.Select(m => m.MaterialId).ToList();

            // --- 3. Tải chi tiết Material và Material Prices ---
            var materialDetails = await _unitOfWork.Repository<Material>()
                .ListAsync(filter: m => materialIds.Contains(m.Id));

            var materialDetailsMap = materialDetails.ToDictionary(m => m.Id, m => m);

            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom <= today
            );

            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => new { p.PricePerMaterial, p.ValidFrom });
            
            // --- 4. Tính toán Chi phí từng Plot trước, sau đó tổng hợp lên Group ---
            
            decimal totalGroupCost = 0M;
            var priceWarnings = new List<string>();
            var plotCostDetails = new List<PlotCostDetailResponse>();
            
            // Dictionary để tổng hợp vật tư: MaterialId -> { TotalRequiredQuantity, TotalPackages, TotalCost }
            var materialAggregation = new Dictionary<Guid, (decimal TotalQuantity, decimal TotalPackages, decimal TotalCost)>();

            // --- 4a. Tính chi phí cho TỪNG PLOT ---
            foreach (var groupPlot in group.GroupPlots)
            {
                var plot = groupPlot.Plot;
            {
                var plotArea = plot.Area;
                if (plotArea <= 0) continue;
                
                decimal plotTotalCost = 0M;
                
                // Tính chi phí vật tư cho plot này
                foreach (var input in aggregatedMaterials)
                {
                    if (!materialDetailsMap.TryGetValue(input.MaterialId, out var materialDetail)) continue;
                    if (!materialPriceMap.TryGetValue(input.MaterialId, out var priceInfo)) continue;
                    
                    var amountPerPackage = materialDetail.AmmountPerMaterial.GetValueOrDefault(1M);
                    if (amountPerPackage <= 0) amountPerPackage = 1M;
                    
                    var quantityPerHa = input.Quantity;
                    var effectivePricePerPackage = priceInfo.PricePerMaterial;
                    
                    // Số lượng cần thiết cho plot này
                    var plotQuantityRequired = quantityPerHa * plotArea;
                    
                    // Số gói cần mua cho plot này
                    // Nếu IsPartition = true (vật tư có thể chia nhỏ), không cần làm tròn lên
                    var plotPackagesNeeded = materialDetail.IsPartition 
                        ? plotQuantityRequired / amountPerPackage 
                        : Math.Ceiling(plotQuantityRequired / amountPerPackage);
                    
                    // Chi phí vật tư này cho plot
                    var plotMaterialCost = plotPackagesNeeded * effectivePricePerPackage;
                    
                    plotTotalCost += plotMaterialCost;
                    
                    // Tổng hợp vào dictionary để tính MaterialCostDetails
                    if (!materialAggregation.ContainsKey(input.MaterialId))
                    {
                        materialAggregation[input.MaterialId] = (0M, 0M, 0M);
                    }
                    
                    var current = materialAggregation[input.MaterialId];
                    materialAggregation[input.MaterialId] = (
                        current.TotalQuantity + plotQuantityRequired,
                        current.TotalPackages + plotPackagesNeeded,
                        current.TotalCost + plotMaterialCost
                    );
                }
                
                // Lưu chi tiết plot
                plotCostDetails.Add(new PlotCostDetailResponse
                {
                    PlotId = plot.Id,
                    PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                    PlotArea = plotArea,
                    AreaRatio = plotArea / effectiveTotalArea,
                    AllocatedCost = plotTotalCost
                });
                
                // Cộng vào tổng chi phí group
                totalGroupCost += plotTotalCost;
            }
            }
            
            // --- 4b. Tạo MaterialCostDetails từ dữ liệu đã tổng hợp ---
            var materialCostDetails = new List<MaterialCostDetailResponse>();
            
            foreach (var input in aggregatedMaterials)
            {
                if (!materialDetailsMap.TryGetValue(input.MaterialId, out var materialDetail)) continue;
                if (!materialPriceMap.TryGetValue(input.MaterialId, out var priceInfo)) continue;
                if (!materialAggregation.TryGetValue(input.MaterialId, out var aggregated)) continue;
                
                materialCostDetails.Add(new MaterialCostDetailResponse
                {
                    MaterialId = input.MaterialId,
                    MaterialName = materialDetail.Name,
                    Unit = materialDetail.Unit,
                    RequiredQuantity = aggregated.TotalQuantity,
                    PackagesNeeded = aggregated.TotalPackages,
                    EffectivePricePerPackage = priceInfo.PricePerMaterial,
                    MaterialTotalCost = aggregated.TotalCost,
                    PriceValidFrom = priceInfo.ValidFrom
                });
            }
            
            // --- 6. Xây dựng Phản hồi ---

            var response = new CalculateGroupMaterialCostResponse
            {
                GroupId = request.GroupId,
                TotalGroupArea = effectiveTotalArea,
                TotalGroupCost = totalGroupCost, // TỔNG CHI PHÍ CUỐI CÙNG (Đã làm tròn gói)
                MaterialCostDetails = materialCostDetails,
                PlotCostDetails = plotCostDetails,
                PriceWarnings = priceWarnings
            };

            var message = priceWarnings.Any() 
                ? $"Successfully calculated cost with {priceWarnings.Count} warning(s)." 
                : "Successfully calculated group material cost.";

            _logger.LogInformation("Calculated total group cost for Group ID {GId}: {Cost}. Plots analyzed: {PlotCount}", 
                request.GroupId, totalGroupCost, plotCostDetails.Count);

            return Result<CalculateGroupMaterialCostResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating group material cost for Group ID {GId}", request.GroupId);
            return Result<CalculateGroupMaterialCostResponse>.Failure("An error occurred during cost calculation.", "CostCalculationFailed");
        }
    }
}