using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.UavVendorRequests
{
    public class UavVendorRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string VendorName { get; set; }
        [MaxLength(100)]
        public string? BusinessRegistrationNumber { get; set; }
        public decimal ServiceRatePerHa { get; set; }
        public int FleetSize { get; set; }
        [Column(TypeName = "decimal(8,2)")]
        public decimal? ServiceRadius { get; set; }
        [Column(TypeName = "jsonb")]
        public string? EquipmentSpecs { get; set; }
        [Column(TypeName = "jsonb")]
        public string? OperatingSchedule { get; set; }
    }
}
