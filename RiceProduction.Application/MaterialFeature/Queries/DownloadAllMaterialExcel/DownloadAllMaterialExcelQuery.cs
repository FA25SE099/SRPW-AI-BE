using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel
{
    public class DownloadAllMaterialExcelQuery : IRequest<Result<IActionResult>>
    {
        public DateTime InputDate { get; set; }
    }
}
