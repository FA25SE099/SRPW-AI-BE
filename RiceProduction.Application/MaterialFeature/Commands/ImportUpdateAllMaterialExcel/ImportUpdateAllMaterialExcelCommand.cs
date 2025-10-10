using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportUpdateAllMaterialExcel
{
    public class ImportUpdateAllMaterialExcelCommand : IRequest<Result<List<MaterialResponse>>>
    {
        public IFormFile ExcelFile { get; set; }
        public DateTime ImportDate { get; set; }
    }
}
