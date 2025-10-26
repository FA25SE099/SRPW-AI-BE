using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class SupervisorDTO
    {
        public Guid SupervisorId { get; set; }
        public int MaxFarmerCapacity { get; set; } = 10;
        public int CurrentFarmerCount { get; set; } = 0;
        public ICollection<GroupDTO> SupervisedGroups { get; set; } = new List<GroupDTO>();
        public ICollection<CultivationTaskDTO> AssignedTasks { get; set; } = new List<CultivationTaskDTO>();
        public ICollection<SupervisorFarmerAssignmentDTO> SupervisorAssignments { get; set; } = new List<SupervisorFarmerAssignmentDTO>();
    }
}
