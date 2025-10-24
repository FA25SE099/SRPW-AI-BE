using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models
{
    public class MaterialDTO
    {
        public Guid MaterialId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? AmmountPerMaterial { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerMaterial { get; set; }
    }

    public class MaterialPriceDTO
    {
        public Guid MaterialPriceId { get; set; }
        public decimal PricePerMaterial { get; set; }
    }
}
