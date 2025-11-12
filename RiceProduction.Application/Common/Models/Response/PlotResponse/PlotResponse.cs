using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response.PlotResponse
{
    public class PlotResponse
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
    public class PlotInVietnamese
    {
        [JsonPropertyName("Mã thửa đất")]
        public Guid PlotId { get; set; }
        [JsonPropertyName("Mã Nông Dân")]
        public Guid FarmerId { get; set; }
        [JsonPropertyName("Tên Nông Dân")]
        public string? FarmerName { get; set; }
        [JsonPropertyName("Mã Nhóm")]
        public Guid? GroupId { get; set; }
        [JsonPropertyName("Số thửa")]
        public int? SoThua { get; set; }
        [JsonPropertyName("Số tờ")]
        public int? SoTo { get; set; }
        [JsonPropertyName("Diện tích")]
        public decimal Area { get; set; }
        [JsonPropertyName("Loại đất")]
        public string? SoilType { get; set; }
        [JsonPropertyName("Trạng thái")]
        public PlotStatus Status { get; set; }
        [JsonPropertyName("Tên giống lúa")]
        public string VarietyName { get; set; }
    }
    public class BoundaryResponse
    {
        public Guid PlotId { get; set; }
        public string Boundary { get; set; }
        public string Coordinate { get; set; }
    }
    
}
