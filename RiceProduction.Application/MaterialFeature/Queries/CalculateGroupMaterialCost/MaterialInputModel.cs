using System;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;

public class MaterialInputModel
{
    [Required]
    public Guid MaterialId { get; set; }
    
    /// <summary>
    /// Số lượng yêu cầu trên mỗi hecta (Quantity/ha).
    /// </summary>
    [Required]
    public decimal Quantity { get; set; }
}