using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;

namespace RiceProduction.Application.MaterialFeature.Commands.ImportUpsertMaterialExcel
{
    public class ImportUpsertMaterialExcelCommand : IRequest<Result<MaterialUpsertResult>>
    {
        public IFormFile ExcelFile { get; set; }
        public DateTime ImportDate { get; set; }
    }

    public class MaterialUpsertResult
    {
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<MaterialResponse> Materials { get; set; } = new List<MaterialResponse>();
    }
}

