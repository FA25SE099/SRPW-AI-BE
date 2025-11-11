using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.GetOutOfSeason
{
    public class GetPlotOutSeasonQueries : IRequest<Result<IEnumerable<PlotDTO>>>
    {
        public DateTime? CurrentDate { get; set; }
        public string? SearchTerm { get; set; }

    }
}
