using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.FarmerFeature.Queries
{
    public class DownloadAllFarmerExcelQuery : IRequest<Result<IActionResult>>
    {
        public DateTime? InputDate { get; set; }
    }
}
