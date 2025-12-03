using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Commands.UpdateCoordinate
{
    public class UpdateCoordinateCommand : IRequest<Result<bool>>
    {
        public class UpdateCoordinateRequest
        {
            public string CoordinateGeoJson { get; set; } = string.Empty;
            public string? Notes { get; set; }
        }
        public Guid PlotId { get; set; }
        public string CoordinateGeoJson { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
