using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetDetailById;

namespace RiceProduction.Application.PlotFeature.Queries.GetDetail
{
    public class GetPlotDetailQueries : IRequest<Result<PlotDetailDTO>>
    {
        public Guid PlotId { get; set; }
        public GetPlotDetailQueries (Guid plotId)
        {
            PlotId = plotId;
        }
    }
}
