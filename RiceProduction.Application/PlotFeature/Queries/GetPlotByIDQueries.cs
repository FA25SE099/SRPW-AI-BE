using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries
{
    public class GetPlotByIDQueries : IRequest<PlotDTO>
    {
        public Guid PlotId { get; set; }
        public GetPlotByIDQueries(Guid id)
        {
            PlotId = id;
        }
    }
}
