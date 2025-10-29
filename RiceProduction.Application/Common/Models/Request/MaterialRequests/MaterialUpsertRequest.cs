using RiceProduction.Domain.Enums;
using System.Text.Json.Serialization;

namespace RiceProduction.Application.Common.Models.Request.MaterialRequests
{
    public class MaterialUpsertRequest
    {
        [JsonPropertyName("Mã sản phẩm")]
        public Guid? MaterialId { get; set; }
        
        [JsonPropertyName("Tên sản phẩm")]
        public string Name { get; set; }
        
        [JsonPropertyName("Loại sản phẩm")]
        public MaterialType Type { get; set; }
        
        [JsonPropertyName("Dung tích mỗi sản phẩm")]
        public decimal? AmmountPerMaterial { get; set; }
        
        [JsonPropertyName("Đơn vị dung tích")]
        public string Unit { get; set; }
        
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

