using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models
{
    public class FarmerImportDto
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? FarmCode { get; set; }
        
        /// <summary>
        /// Number of plots the farmer will have - used to generate plot import template
        /// </summary>
        public int? NumberOfPlots { get; set; }
        
        /// <summary>
        /// Optional email address for sending account credentials
        /// </summary>
        public string? Email { get; set; }
    }

    public class ImportFarmerResult
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<ImportError> Errors { get; set; } = new();
        public List<ImportedFarmerData> ImportedFarmers { get; set; } = new List<ImportedFarmerData>();
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
        public int? NumberOfPlots { get; set; }
        public string? Email { get; set; }
        public string? TempPassword { get; set; }
    }
}
