using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.ClusterResponses
{
    public class ClusterResponse
    {
        public Guid ClusterId { get; set; }
        public string ClusterName { get; set; } = string.Empty;
        public Guid? ClusterManagerId { get; set; }
        public Guid? AgronomyExpertId { get; set; }
        public string? ClusterManagerName { get; set; }
        public string? ClusterManagerPhoneNumber { get; set; }
        public string? ClusterManagerEmail { get; set; }
        public string? AgronomyExpertName { get; set; }
        public string? AgronomyExpertPhoneNumber { get; set; }
        public string? AgronomyExpertEmail { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? Area { get; set; }
        
        // Supervisor information
        public List<SupervisorSummary>? Supervisors { get; set; }
    }

    public class SupervisorSummary
    {
        public Guid SupervisorId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public int CurrentFarmerCount { get; set; }
        public int MaxFarmerCapacity { get; set; }
        public DateTime? AssignedDate { get; set; }
    }
}
