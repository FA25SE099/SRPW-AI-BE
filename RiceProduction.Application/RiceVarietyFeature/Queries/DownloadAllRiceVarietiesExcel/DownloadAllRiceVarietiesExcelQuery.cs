using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.DownloadAllRiceVarietiesExcel
{
    public class DownloadAllRiceVarietiesExcelQuery : IRequest<Result<FileExportResult>>
    {
        public DateTime InputDate { get; set; }
        public Guid? CategoryId { get; set; }
        public bool? IsActive { get; set; }
    }
}

