using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RiceProduction.Application.Common.Models.Request.MaterialRequests
{
    public class ImportUpdateMaterialExcel
    {
        public IFormFile ExcelFile { get; set; }
        public DateTime ImportDate { get; set; }
    }
}
