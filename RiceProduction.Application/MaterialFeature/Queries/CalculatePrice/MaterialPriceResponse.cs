using System.ComponentModel.DataAnnotations;
using System;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculatePrice;

public class MaterialPriceResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public decimal TotalCalculatedCost { get; set; }
    
    public decimal EffectiveUnitPrice { get; set; }
}