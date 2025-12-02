using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.GetAll
{
    public class GetAllPlotQueriesHandler : IRequestHandler<GetAllPlotQueries ,PagedResult<IEnumerable<PlotDTO>>>
    {
        private readonly ILogger<GetAllPlotQueriesHandler> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAllPlotQueriesHandler(ILogger<GetAllPlotQueriesHandler> logger, IUnitOfWork unitofwork, IMapper mapper)
        {
            _logger = logger;
            _unitOfWork = unitofwork;
            _mapper = mapper;
        }

        public async Task<PagedResult<IEnumerable<PlotDTO>>> Handle(GetAllPlotQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting all page: {PageNumber}, PageSize: {PageSize}"
                    , request.PageNumber, request.PageSize);
                Expression<Func<Plot, bool>>? predicate = null;
                
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    predicate = p => p.Status == PlotStatus.Active &&
                    (p.SoThua.HasValue && p.SoThua.ToString().Contains(request.SearchTerm) ||
                    p.SoTo.HasValue && p.SoTo.ToString().Contains(request.SearchTerm) ||
                    p.Farmer != null && p.Farmer.FullName.Contains(request.SearchTerm));
                }
                
                var (items, totalCount) = await _unitOfWork.PlotRepository
                    .GetAllPlotPagedAsync(request.PageNumber, request.PageSize, predicate, cancellationToken);
                var plotDTOs = _mapper.Map<IEnumerable<PlotDTO>>(items);
                return PagedResult<IEnumerable<PlotDTO>>.Success
                    (data: plotDTOs,
                    currentPage: request.PageNumber,
                    pageSize: request.PageSize,
                    totalCount: totalCount,
                    message: "Plots retrieved successfully");
               

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting all plots");
                return PagedResult<IEnumerable<PlotDTO>>.Failure(
                    error: ex.Message,
                    message: "Failed to retrieve plots"
                );
            }
        }
    }
}
