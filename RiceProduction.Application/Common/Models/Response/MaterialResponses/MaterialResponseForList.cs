using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.MaterialResponses;

public class MaterialResponseForList
{
    public Guid MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MaterialType Type { get; set; }
    public decimal? AmmountPerMaterial { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Showout { get; set; } = string.Empty;
    public decimal PricePerMaterial { get; set; }
    public string? Description { get; set; }
    public List<string>? ImgUrls { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsActive { get; set; }
}
