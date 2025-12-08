using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterFeature.Commands.UpdateCluster
{
    public class UpdateClusterCommand : IRequest<Result<Guid>>
    {
        public Guid ClusterId { get; set; }
        [Required]
        [MaxLength(255)]
        public string ClusterName { get; set; } = string.Empty;

        public Guid? ClusterManagerId { get; set; }
        public Guid? AgronomyExpertId { get; set; }
        
        /// <summary>
        /// List of Supervisor IDs to assign to this cluster
        /// Replaces existing supervisor assignments
        /// </summary>
        public List<Guid>? SupervisorIds { get; set; }
    }
}
