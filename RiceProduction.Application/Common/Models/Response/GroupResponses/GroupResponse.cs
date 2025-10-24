using NetTopologySuite.Geometries;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.GroupResponses
{
    public class GroupResponse
    {
        [Required]
        public Guid ClusterId { get; set; }

        public Guid? SupervisorId { get; set; }
        public string? SupervisorName { get; set; }

        public Guid? RiceVarietyId { get; set; }

        public Guid? SeasonId { get; set; }

        public DateTime? PlantingDate { get; set; }

        public GroupStatus Status { get; set; } = GroupStatus.Draft;

        public bool IsException { get; set; } = false;

        public string? ExceptionReason { get; set; }

        /// <summary>
        /// Estimated date when group is ready for UAV service
        /// </summary>
        public DateTime? ReadyForUavDate { get; set; }

        [Column(TypeName = "geometry(Polygon,4326)")]
        public string? Area { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TotalArea { get; set; }
    }
}
