using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType
{
    public class GetAllMaterialByTypeQuery : IRequest<PagedResult<List<MaterialResponse>>>
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public MaterialType MaterialType { get; set; }
    }
}
