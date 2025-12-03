using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq;
using System;
using System.Collections.Generic;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculatePrice;
public class CalculateMaterialPriceQueryHandler : IRequestHandler<CalculateMaterialPriceQuery, Result<MaterialPriceResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateMaterialPriceQueryHandler> _logger;

    public CalculateMaterialPriceQueryHandler(IUnitOfWork unitOfWork, ILogger<CalculateMaterialPriceQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<MaterialPriceResponse>> Handle(CalculateMaterialPriceQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.UtcNow;

            // 1. Tải chi tiết Material (cần để lấy AmmountPerMaterial - Lượng/gói)
            var material = await _unitOfWork.Repository<Material>().FindAsync(m => m.Id == request.MaterialId);
            if (material == null)
            {
                return Result<MaterialPriceResponse>.Failure("Material not found.", "MaterialNotFound");
            }
            
            // 2. Tải MaterialPrice có hiệu lực mới nhất
            var latestPrice = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => p.MaterialId == request.MaterialId && p.ValidFrom <= today,
                orderBy: q => q.OrderByDescending(p => p.ValidFrom)
            );
            
            var effectivePrice = latestPrice.FirstOrDefault();

            if (effectivePrice == null)
            {
                _logger.LogWarning("No effective price found for Material {MaterialId}", request.MaterialId);
                return Result<MaterialPriceResponse>.Failure("No valid price found for this material.", "PriceNotFound");
            }

            // --- 3. Tính toán chi phí dựa trên đơn vị đóng gói (PACKAGING LOGIC) ---
            
            // Lượng vật tư trong 1 đơn vị đóng gói (Ví dụ: 50 kg/bao)
            var amountPerPackage = material.AmmountPerMaterial.GetValueOrDefault(1M);
            var requiredQuantity = request.Quantity;

            if (amountPerPackage <= 0)
            {
                // Nếu dữ liệu không hợp lệ, giả định 1 đơn vị vật tư tương đương 1 đơn vị giá (1:1)
                amountPerPackage = 1M;
            }

            // Tính số lượng đơn vị đóng gói (bao/thùng) cần mua (Làm tròn lên)
            // Ví dụ: 10 kg / 3 kg/bao = 3.33 -> 4 bao
            var packagesNeeded = Math.Ceiling(requiredQuantity / amountPerPackage);

            // Tổng số lượng thực tế được tính giá (Total Quantity To Bill)
            // Lượng này có thể lớn hơn RequiredQuantity (Ví dụ: 4 bao * 3 kg/bao = 12 kg)
            var totalQuantityToBill = packagesNeeded * amountPerPackage;
            
            // Tổng chi phí = Số lượng gói cần mua * Giá/gói
            var totalCost = packagesNeeded * effectivePrice.PricePerMaterial;


            var response = new MaterialPriceResponse
            {
                MaterialId = material.Id,
                MaterialName = material.Name,
                Unit = material.Unit,
                EffectiveUnitPrice = effectivePrice.PricePerMaterial,
                TotalCalculatedCost = totalCost
            };

            _logger.LogInformation("Calculated cost for {Qty} {Unit} of {Name}. Packages needed: {Packages}. Total cost: {Cost}", 
                request.Quantity, material.Unit, material.Name, packagesNeeded, totalCost);
            
            return Result<MaterialPriceResponse>.Success(response, "Price calculated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating material price for {MaterialId}", request.MaterialId);
            return Result<MaterialPriceResponse>.Failure("An error occurred during price calculation.", "PriceCalculationFailed");
        }
    }
}