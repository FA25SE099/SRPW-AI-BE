using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.PlotFeature.Commands.EditPlot
{
    public class EditPlotCommand : IRequest<Result<UpdatePlotRequest>>
    {
        public required UpdatePlotRequest Request { get; set; }
    }
}
