using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.MaterialResponses;

/// <summary>
/// Task cost breakdown with its materials
/// </summary>
public class TaskCostBreakdown
{
    public string TaskName { get; set; } = string.Empty;
    public string? TaskDescription { get; set; }
    public decimal TotalTaskCost { get; set; }
    public List<MaterialCostItem> Materials { get; set; } = new();
}

public class CalculateMaterialsCostByAreaResponse
{
    /// <summary>
    /// Input area in hectares
    /// </summary>
    public decimal Area { get; set; }

    /// <summary>
    /// Total cost for all materials per hectare
    /// </summary>
    public decimal TotalCostPerHa { get; set; }

    /// <summary>
    /// Total cost for all materials for the given area
    /// </summary>
    public decimal TotalCostForArea { get; set; }

    /// <summary>
    /// Total cost for all task materials
    /// </summary>
    public decimal TotalTaskMaterialsCost { get; set; }

    /// <summary>
    /// All materials aggregated (old response format for backward compatibility)
    /// </summary>
    public List<MaterialCostItem> MaterialCostItems { get; set; } = new();

    /// <summary>
    /// Cost breakdown by task
    /// </summary>
    public List<TaskCostBreakdown> TaskCostBreakdowns { get; set; } = new();

    /// <summary>
    /// Warnings about missing or invalid prices
    /// </summary>
    public List<string> PriceWarnings { get; set; } = new();
}

public class MaterialCostItem
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Quantity per hectare (input)
    /// </summary>
    public decimal QuantityPerHa { get; set; }

    /// <summary>
    /// Total quantity needed for the area before rounding
    /// </summary>
    public decimal TotalQuantityNeeded { get; set; }

    /// <summary>
    /// Amount per package/material unit
    /// </summary>
    public decimal AmountPerMaterial { get; set; }

    /// <summary>
    /// Number of packages needed (ceiled)
    /// </summary>
    public decimal PackagesNeeded { get; set; }

    /// <summary>
    /// Actual quantity after ceiling to packages
    /// </summary>
    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// Current price per package/material
    /// </summary>
    public decimal PricePerMaterial { get; set; }

    /// <summary>
    /// Total cost for this material
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Cost per hectare for this material
    /// </summary>
    public decimal CostPerHa { get; set; }

    /// <summary>
    /// Price validity date
    /// </summary>
    public DateTime? PriceValidFrom { get; set; }
}
