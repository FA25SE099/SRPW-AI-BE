using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Request.PlotRequest
{
    public class PlotRequest
    {
        public Guid PlotId { get; set; }
        public Guid FarmerId { get; set; }
        public string? FarmerName { get; set; }
        public Guid? GroupId { get; set; }
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        public decimal Area { get; set; }
        public string? SoilType { get; set; }
        public PlotStatus Status { get; set; }
        public string VarietyName { get; set; }
    }
    public class BoundaryUpdateRequest
    {
        public Guid FarmerId { get; set; }
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        public string Boundary {  get; set; }
        public string Coordinate { get; set; }
    }
}
