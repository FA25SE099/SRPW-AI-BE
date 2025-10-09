using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models
{
    public class PlotDTO 
    {
        public Guid Id { get; set; }
        public Guid FarmerId { get; set; }
        public string? FarmerName { get; set; }
        public Guid? GroupId { get; set; }
        public string? BoundaryGeoJson { get; set; }
        public string? CoordinateGeoJson { get; set; }
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        public decimal Area { get; set; }
        public string? SoilType { get; set; }
        public PlotStatus Status { get; set; } 
        public string VarietyName { get; set; } 
    }
}
