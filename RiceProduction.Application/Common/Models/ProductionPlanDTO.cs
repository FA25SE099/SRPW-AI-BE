using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;


namespace RiceProduction.Application.Common.Models
{
    public class ProductionPlanDTO
    {
        public Guid ProductionPlanId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? StandardPlanId { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public DateTime BasePlantingDate { get; set; }
        public Domain.Enums.TaskStatus Status { get; set; }
        public decimal? TotalArea { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }
        public Guid? SubmittedBy { get; set; }
        public Guid? CurrentProductionStageId { get; set; }
        public GroupDTO? Group { get; set; }
        public SupervisorDTO? Submitter { get; set; }
        public AgronomyExpertDTO? Approver { get; set; }
        public ICollection<ProductionStageDTO> CurrentProductionStages { get; set; } = new List<ProductionStageDTO>();
    }
}
