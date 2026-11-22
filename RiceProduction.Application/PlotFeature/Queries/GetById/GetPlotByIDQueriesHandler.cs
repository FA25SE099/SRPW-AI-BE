using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.GetById
{
    public class GetPlotByIDQueriesHandler : IRequestHandler<GetPlotByIDQueries, PlotDTO>
    {
        private readonly ILogger<GetPlotByIDQueriesHandler> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public GetPlotByIDQueriesHandler(IUnitOfWork unitOfWork, ILogger<GetPlotByIDQueriesHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<PlotDTO> Handle(GetPlotByIDQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving Plot with id:{PlotId}", request.PlotId);
                var plot = await _unitOfWork.PlotRepository.GetPlotByIDAsync(request.PlotId, cancellationToken);
                if (plot == null)
                {
                    _logger.LogWarning("Farmer with Id:{PlotId} not found", request.PlotId);
                    return null;
                }
                var plotDTOs = plot.ToDto();
                _logger.LogInformation(
                    "Successfully retrieve plo with ID: {PlotId})",
                    plot.Id
                    );
                return plotDTOs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving plot: {PlotId}", request.PlotId);
                throw;
            }
        }
    }
}
