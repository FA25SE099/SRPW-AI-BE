using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterFeature.Commands.CreateCluster
{
    public class CreateClusterCommand : IRequest<Result<Guid>>
    {
        [Required]
        [MaxLength(255)]
        public string ClusterName { get; set; } = string.Empty;

        public Guid? ClusterManagerId { get; set; }
        public Guid? AgronomyExpertId { get; set; }
    }
}
