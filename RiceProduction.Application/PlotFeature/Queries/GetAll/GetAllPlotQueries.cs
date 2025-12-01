using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.GetAll
{
    public class GetAllPlotQueries : IRequest<PagedResult<IEnumerable<PlotDTO>>>
    {
        public Guid? ClusterManagerId { get; set; }

        public int PageSize { get; set; } = 10;
        public int PageNumber { get; set; } = 1;
        public string? SearchTerm { get; set; } 

    }
}
