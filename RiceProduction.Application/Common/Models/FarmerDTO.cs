using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class FarmerDTO
    {
        public Guid FarmerId { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }  
        public bool IsActive { get; set; } = true;
        public bool IsVerified { get; set; } = false;
        public DateTime? LastActivityAt { get; set; }
        public string? FarmCode { get; set; }
        public int PlotCount { get; set; }
    }
    public class FarmerExcelDTO
    {
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FarmCode { get; set; }
        public int PlotCount { get; set; }
    }

}
