using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;
public class FarmLogMaterialRequest
{
    [Required]
    public Guid MaterialId { get; set; }
    
    [Required]
    public decimal ActualQuantityUsed { get; set; }
    
    public string? Notes { get; set; }
}