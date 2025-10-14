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
        public Guid? RiceVarietyId { get; set; }
        public Guid? SeasonId { get; set; }
        public DateTime? PlantingDate { get; set; }
        public GroupStatus Status { get; set; } = GroupStatus.Active;
        public bool IsException { get; set; } = false;
        public string? ExceptionReason { get; set; }
        public DateTime? ReadyForUavDate { get; set; }
        public string? Area { get; set; }
        public decimal? TotalArea { get; set; }
        public ClusterDTO Cluster { get; set; } = null!;
        public SupervisorDTO? Supervisor { get; set; }
        public RiceVarietyDTO? RiceVariety { get; set; }
        public ICollection<PlotDTO> Plots { get; set; } = new List<PlotDTO>();
        public ICollection<ProductionPlanDTO> ProductionPlans { get; set; } = new List<ProductionPlanDTO>();

    }
}
