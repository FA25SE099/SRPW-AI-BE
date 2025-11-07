using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.ExportFarmerTemplateExcel
{
    public class ExportFarmerTemplateQueries : IRequest<Result<IActionResult>>
    {
        public ExportFarmerTemplateQueries()
        {
        }
    }
}
