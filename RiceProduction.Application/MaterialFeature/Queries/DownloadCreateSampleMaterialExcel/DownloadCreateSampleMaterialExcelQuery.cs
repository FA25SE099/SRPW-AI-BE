using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.DownloadCreateSampleMaterialExcel
{
    public class DownloadCreateSampleMaterialExcelQuery : IRequest<Result<FileExportResult>>
    {
        public DownloadCreateSampleMaterialExcelQuery()
        {
        }
    }
}
