using NetTopologySuite.Geometries;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.PlotRequests
{
    public class UpdatePlotRequest
    {
        [Required]
        public Guid PlotId { get; set; }
        [Required]
        public Guid FarmerId { get; set; }

        public Guid? GroupId { get; set; }

        [Required]
        public string Boundary { get; set; } = null!;
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Area { get; set; }

        [MaxLength(100)]
        public string? SoilType { get; set; }

        public string? Coordinate { get; set; }

        public PlotStatus Status { get; set; } = PlotStatus.Active;
    }
}
