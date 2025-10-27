using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class SupervisorFarmerAssignmentDTO
    {
        public Guid SupervisorFarmerAssignmentId { get; set; }
        public Guid SupervisorId { get; set; }
        public Guid FarmerId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public string? AssignmentNotes { get; set; }
        public int Priority { get; set; } = 1;
        public SupervisorDTO Supervisor { get; set; } = null!;
        public FarmerDTO Farmer { get; set; } = null!;
    }
}
