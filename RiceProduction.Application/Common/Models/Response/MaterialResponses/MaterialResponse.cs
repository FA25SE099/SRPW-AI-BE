using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.MaterialResponses
{
    public class MaterialResponse
    {
        public Guid MaterialId { get; set; }
        
        public string Name { get; set; }
        
        public MaterialType Type { get; set; }
        
        public decimal? AmmountPerMaterial { get; set; }
        
        public string Unit { get; set; }
        
        public string Showout { get; set; }
        
        public decimal PricePerMaterial { get; set; }
        
        public string? Description { get; set; }
        
        public string? Manufacturer { get; set; }
        
        public bool IsActive { get; set; }
    }
    public class MaterialResponseInVietnamese
    {
        [JsonPropertyName("Mã sản phẩm")]
        public Guid MaterialId { get; set; }
        
        [JsonPropertyName("Tên sản phẩm")]
        public string Name { get; set; }
        
        [JsonPropertyName("Loại sản phẩm")]
        public MaterialType Type { get; set; }
        
        [JsonPropertyName("Dung tích mỗi sản phẩm")]
        public decimal? AmmountPerMaterial { get; set; }
        
        [JsonPropertyName("Đơn vị dung tích")]
        public string Unit { get; set; }
        
        [JsonPropertyName("Dung tích sản phẩm (đã ghép đơn vị)")]
        public string Showout { get; set; }
        
        [JsonPropertyName("Giá mỗi sản phẩm (theo dung tích)")]
        public decimal PricePerMaterial { get; set; }
        
        [JsonPropertyName("Mô tả và ghi chú")]
        public string? Description { get; set; }
        
        [JsonPropertyName("Nhà phân phối")]
        public string? Manufacturer { get; set; }
        
        [JsonPropertyName("Có đang được sử dụng hay không")]
        public bool IsActive { get; set; }
    }
}