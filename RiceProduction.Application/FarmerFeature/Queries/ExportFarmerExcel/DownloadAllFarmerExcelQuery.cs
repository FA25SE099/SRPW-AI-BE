using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerExcel
{
    public class DownloadAllFarmerExcelQuery : IRequest<Result<FileExportResult>>
    {
        public DateTime? InputDate { get; set; }
    }
}
