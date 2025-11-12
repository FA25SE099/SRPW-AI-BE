using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models
{
    public class FarmerImportDto
    {
        public Guid? Id { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string FarmCode { get; set; }
        
        // Plot basic data (without polygon - supervisor will add later)
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        public decimal? PlotArea { get; set; }
        public string? SoilType { get; set; }
    }

    public class ImportFarmerResult
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<ImportError> Errors { get; set; } = new();
        public List<ImportedFarmerData> ImportedFarmers { get; set; } = new List<ImportedFarmerData>();
        public List<Guid> CreatedPlotIds { get; set; } = new List<Guid>(); // Track plots needing polygon assignment
    }

    public class ImportError
    {
        public int RowNumber { get; set; }
        public string FieldName { get; set; }
        public string ErrorMessage { get; set; }    
    }

    public class ImportedFarmerData
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? FarmCode { get; set; }
    }
}
