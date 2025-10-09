using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries
{
    public class GetAllFarmerQueries : IRequest<PagedResult<IEnumerable<FarmerDTO>>>
    {
        public int PageSize { get; set; } = 10;
        public int PageNumber { get; set; } = 1;
        public string? SearchTerm { get; set; }
    }
}
