using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response
{
    public class MaterialResponse
    {
        public string Name { get; set; }
        public MaterialType Type { get; set; }
        public decimal? AmmountPerMaterial { get; set; }
        public string Unit { get; set; }
        public string Showout { get; set; }
        public decimal PricePerMaterial { get; set; }
        public string? Description { get; set; }
        public string? Manufacturer { get; set; }
        public bool IsActive { get; set; }
    }
}
