using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotResponse;

namespace RiceProduction.Application.PlotFeature.Commands.ImportExcel
{
    public class ImportPlotByExcelCommand : IRequest<Result<List<PlotResponse>>>
    {
        public required IFormFile ExcelFile { get; set; }
        public DateTime ImportDate { get; set; }
    }
}
