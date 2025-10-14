using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class ProductionStageDTO
    {
        public Guid ProductionStageId { get; set; }
        public Guid ProductionPlanId { get; set; }
        public string StageName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SequenceOrder { get; set; }
        public int? TypicalDurationDays { get; set; }
        public string? ColorCode { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
        public ICollection<ProductionPlanTaskDTO> ProductionPlanTasks { get; set; } = new List<ProductionPlanTaskDTO>();
        public ProductionPlanDTO ProductionPlan { get; set; }

    }
}
