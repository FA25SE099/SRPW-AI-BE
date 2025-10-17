using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportUpdateAllMaterialExcel
{
    public class ImportUpdateAllMaterialExcelCommand : IRequest<Result<List<MaterialResponse>>>
    {
        public required IFormFile ExcelFile { get; set; }
        public DateTime ImportDate { get; set; }
    }
}
