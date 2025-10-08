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
    }

    public class ImportFarmerResult
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<ImportError> Errors { get; set; } = new();
    }

    public class ImportError
    {
        public int RowNumber { get; set; }
        public string FieldName { get; set; }
        public string ErrorMessage { get; set; }    
    }
}
