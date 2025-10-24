using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class ProductionPlanTaskMaterialDTO
    {
        public Guid ProductionPlanTaskMaterialId { get; set; }
        public Guid ProductionPlanTaskId { get; set; }
        public Guid MaterialId { get; set; }
        public decimal QuantityPerHa { get; set; }
        public decimal? EstimatedAmount { get; set; }
        public ProductionPlanTaskDTO ProductionPlanTask { get; set; } = null!;
        public MaterialDTO Material { get; set; } = null!;
    }
}
