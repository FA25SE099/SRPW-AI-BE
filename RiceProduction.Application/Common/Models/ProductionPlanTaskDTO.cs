using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models
{
    public class ProductionPlanTaskDTO
    {
        public Guid ProductionPlanTaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaskType TaskType { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? ScheduledEndDate { get; set; }
        public Domain.Enums.TaskStatus Status { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        public int SequenceOrder { get; set; }

    }
}
