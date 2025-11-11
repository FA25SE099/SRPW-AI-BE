using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetDetailById;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Queries.GetDetail
{
    public class GetPlotDetailQueriesHandler : IRequestHandler<GetPlotDetailQueries, Result<PlotDetailDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<GetPlotDetailQueriesHandler> _logger;

        public GetPlotDetailQueriesHandler (IUnitOfWork unitOfWork, IMapper mapper, ILogger<GetPlotDetailQueriesHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }
        async Task<Result<PlotDetailDTO>> IRequestHandler<GetPlotDetailQueries, Result<PlotDetailDTO>>.Handle(GetPlotDetailQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Retrieving detail plot data with ID: {PlotId}", request.PlotId);
                var query = _unitOfWork.Repository<Plot>().GetQueryable()
                    .Include(p => p.Farmer)
                    .Include(p => p.Group)
                        .ThenInclude(g => g.ProductionPlans)
                            .ThenInclude(pp => pp.CurrentProductionStages)
                                .ThenInclude(ps => ps.ProductionPlanTasks)
                                    .ThenInclude(t => t.ProductionPlanTaskMaterials)
                     .Include(p => p.Group)
                            .ThenInclude(g => g.RiceVariety)
                    .Include(p => p.PlotCultivations)
                        .ThenInclude(pc => pc.Season)
                     .Include(p => p.PlotCultivations)
                        .ThenInclude(pc => pc.RiceVariety)
                      .Include(p => p.PlotCultivations)
                        .ThenInclude(pc => pc.RiceVariety)
                      .Include(p => p.PlotCultivations)
                        .ThenInclude(c => c.CultivationTasks)
                      .AsSplitQuery()
                    .Where(p => p.Id == request.PlotId);
                var plot = await query.FirstOrDefaultAsync(cancellationToken);

                if (plot == null)
                {
                    _logger.LogWarning("Plot with ID {PlotId} not found", request.PlotId);
                    return Result<PlotDetailDTO>.Failure($"Plot with ID {request.PlotId} not found");
                }

                _logger.LogInformation("Successfully retrieved plot with ID: {PlotId}", plot.Id);

                // Map to DTO
                var plotDTO = _mapper.Map<PlotDetailDTO>(plot);

                return Result<PlotDetailDTO>.Success(
                    plotDTO,
                    "Plot details retrieved successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving plot with ID: {PlotId}", request.PlotId);
                return Result<PlotDetailDTO>.Failure(
                    $"Error retrieving plot details: {ex.Message}");
            }
        }
    }
}
