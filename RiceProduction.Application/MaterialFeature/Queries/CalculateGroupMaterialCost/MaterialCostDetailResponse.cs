using System;
using System.Collections.Generic;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;
/// <summary>
/// Chi tiết chi phí vật tư đã được tính toán cho Group.
/// </summary>
public class MaterialCostDetailResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    
    /// <summary>
    /// Tổng số lượng yêu cầu ban đầu (Q/ha * Tổng Area) cho toàn bộ Group.
    /// </summary>
    public decimal RequiredQuantity { get; set; }
    
    /// <summary>
    /// Số lượng gói/bao cần mua (đã làm tròn lên) cho toàn bộ Group.
    /// </summary>
    public decimal PackagesNeeded { get; set; }
    
    /// <summary>
    /// Giá/gói có hiệu lực.
    /// </summary>
    public decimal EffectivePricePerPackage { get; set; }
    
    /// <summary>
    /// Tổng chi phí cho vật tư này (PackagesNeeded * EffectivePricePerPackage).
    /// </summary>
    public decimal MaterialTotalCost { get; set; }
    public DateTime? PriceValidFrom { get; set; }
}

/// <summary>
/// Chi phí phân bổ cho từng Plot trong Group.
/// </summary>
public class PlotCostDetailResponse
{
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }
    
    /// <summary>
    /// Tỷ lệ diện tích của Plot so với tổng diện tích Group.
    /// </summary>
    public decimal AreaRatio { get; set; }
    
    /// <summary>
    /// Chi phí vật tư được phân bổ cho Plot này (tính theo tỷ lệ chi phí gói).
    /// </summary>
    public decimal AllocatedCost { get; set; }
}

/// <summary>
/// Task cost breakdown with its materials
/// </summary>
public class TaskCostBreakdown
{
    public string TaskName { get; set; } = string.Empty;
    public string? TaskDescription { get; set; }
    public decimal TotalTaskCost { get; set; }
    public List<MaterialCostDetailResponse> Materials { get; set; } = new();
}

/// <summary>
/// Seed service cost breakdown
/// </summary>
public class SeedServiceCostBreakdown
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal PackagesNeeded { get; set; }
    public decimal EffectivePricePerPackage { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public DateTime? PriceValidFrom { get; set; }
}

/// <summary>
/// Phản hồi cuối cùng chứa tổng chi phí và chi tiết phân bổ.
/// </summary>
public class CalculateGroupMaterialCostResponse
{
    public Guid GroupId { get; set; }
    public decimal TotalGroupArea { get; set; }

    /// <summary>
    /// Tổng chi phí vật tư của toàn bộ Group (tính theo số gói làm tròn).
    /// </summary>
    public decimal TotalGroupCost { get; set; }

    /// <summary>
    /// Total cost for all task materials
    /// </summary>
    public decimal TotalTaskMaterialsCost { get; set; }

    /// <summary>
    /// Total cost for all seed services
    /// </summary>
    public decimal TotalSeedServicesCost { get; set; }

    /// <summary>
    /// All materials aggregated (old response format for backward compatibility)
    /// </summary>
    public List<MaterialCostDetailResponse> MaterialCostDetails { get; set; } = new();

    /// <summary>
    /// Cost breakdown by task
    /// </summary>
    public List<TaskCostBreakdown> TaskCostBreakdowns { get; set; } = new();

    /// <summary>
    /// Cost breakdown for seed services
    /// </summary>
    public List<SeedServiceCostBreakdown> SeedServiceCostBreakdowns { get; set; } = new();

    public List<PlotCostDetailResponse> PlotCostDetails { get; set; } = new();
    public List<string> PriceWarnings { get; set; } = new();
}