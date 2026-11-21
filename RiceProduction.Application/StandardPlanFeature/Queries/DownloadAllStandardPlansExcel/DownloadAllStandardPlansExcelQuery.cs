using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.StandardPlanFeature.Queries.DownloadAllStandardPlansExcel
{
    public class DownloadAllStandardPlansExcelQuery : IRequest<Result<FileExportResult>>
    {
        public DateTime InputDate { get; set; }
        public Guid? CategoryId { get; set; }
        public bool? IsActive { get; set; }
    }
}

