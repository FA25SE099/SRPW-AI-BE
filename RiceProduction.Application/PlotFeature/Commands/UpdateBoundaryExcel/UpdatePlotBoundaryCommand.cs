using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotResponse;

namespace RiceProduction.Application.PlotFeature.Commands.UpdateBoundaryExcel
{
    public class UpdatePlotBoundaryCommand : IRequest<Result<List<BoundaryResponse>>>
    {
        public required IFormFile ExcelFile { get; set; }
        public DateTime ImportDate { get; set; }
    }
}
