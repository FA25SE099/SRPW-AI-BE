using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.DownloadCreateSampleMaterialExcel
{
    public class DownloadCreateSampleMaterialExcelQuery : IRequest<Result<IActionResult>>
    {
        public DownloadCreateSampleMaterialExcelQuery()
        {
        }
    }
}
