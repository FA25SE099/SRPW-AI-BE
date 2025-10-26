using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models
{
    public class CultivationTaskDTO
    {
        public Guid CultivationTaskId { get; set; }
        public Guid ProductionPlanTaskId { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public Guid? AssignedToVendorId { get; set; }
        public int? ExecutionOrder { get; set; }
        public bool IsContingency { get; set; } = false;
        public string? ContingencyReason { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public decimal ActualMaterialCost { get; set; } = 0;
        public decimal ActualServiceCost { get; set; } = 0;
        public int CompletionPercentage { get; set; } = 0;
        public DateTime? CompletedAt { get; set; }
        public Guid? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? WeatherConditions { get; set; }
        public string? InterruptionReason { get; set; }

    }
}
