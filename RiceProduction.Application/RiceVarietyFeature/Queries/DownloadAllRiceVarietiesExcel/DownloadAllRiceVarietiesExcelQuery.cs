using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Queries.DownloadAllRiceVarietiesExcel
{
    public class DownloadAllRiceVarietiesExcelQuery : IRequest<Result<IActionResult>>
    {
        public DateTime InputDate { get; set; }
        public Guid? CategoryId { get; set; }
        public bool? IsActive { get; set; }
    }
}

