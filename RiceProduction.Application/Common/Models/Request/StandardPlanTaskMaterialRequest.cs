using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request;

public class StandardPlanTaskMaterialRequest
{
    [Required]
    public Guid MaterialId { get; set; }

    [Required]
    public decimal QuantityPerHa { get; set; }
}

