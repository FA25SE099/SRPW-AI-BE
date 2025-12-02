using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models
{
    public class GroupDTO
    {
        public Guid GroupId { get; set; }
        public Guid ClusterId { get; set; }
        public Guid? SupervisorId { get; set; }
        public GroupStatus Status { get; set; } = GroupStatus.Active;
        public string? Area { get; set; }
        public decimal? TotalArea { get; set; }
        public ICollection<ProductionPlanDTO> ProductionPlans { get; set; } = new List<ProductionPlanDTO>();
    }
}
