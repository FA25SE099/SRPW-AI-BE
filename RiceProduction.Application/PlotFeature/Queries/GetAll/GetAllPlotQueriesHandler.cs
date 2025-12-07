using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using AutoMapper;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.GetAll
{
    public class GetAllPlotQueriesHandler : IRequestHandler<GetAllPlotQueries, PagedResult<IEnumerable<PlotDTO>>>
    {
        private readonly ILogger<GetAllPlotQueriesHandler> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAllPlotQueriesHandler(
            ILogger<GetAllPlotQueriesHandler> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<PagedResult<IEnumerable<PlotDTO>>> Handle(GetAllPlotQueries request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                _logger.LogInformation(
                    "Fetching plots - Page: {PageNumber}, PageSize: {PageSize}, SearchTerm: '{SearchTerm}', ClusterManagerId: {ClusterManagerId}",
                    request.PageNumber, request.PageSize, request.SearchTerm?.Trim(), request.ClusterManagerId);

                // Start with base filter: only Active plots
                Expression<Func<Plot, bool>> predicate = p => p.Status == PlotStatus.Active;

                // === 1. Filter by Cluster Manager → Get ClusterId from ClusterManager → Filter Farmer.ClusterId
                if (request.ClusterManagerId.HasValue)
                {
                    var clusterManager = await _unitOfWork.ClusterManagerRepository
                        .GetClusterManagerByIdAsync(request.ClusterManagerId.Value, cancellationToken);

                    if (clusterManager == null)
                    {
                        _logger.LogWarning("ClusterManager Id {ClusterManagerId} not found.", request.ClusterManagerId);
                        return PagedResult<IEnumerable<PlotDTO>>.Success(
                            data: Enumerable.Empty<PlotDTO>(),
                            currentPage: request.PageNumber,
                            pageSize: request.PageSize,
                            totalCount: 0,
                            message: "No cluster manager found with the provided ID.");
                    }

                    var targetClusterId = clusterManager.ClusterId;

                    // Combine: Active + Farmer belongs to this Cluster
                    predicate = predicate.And(p => p.Farmer != null && p.Farmer.ClusterId == targetClusterId);
                }

                // === 2. Search term filter (SoThua, SoTo, Farmer FullName)
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var search = request.SearchTerm.Trim();

                    predicate = predicate.And(p =>
                        (p.SoThua.HasValue && p.SoThua.Value.ToString().Contains(search)) ||
                        (p.SoTo.HasValue && p.SoTo.Value.ToString().Contains(search)) ||
                        (p.Farmer != null && p.Farmer.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    );
                }

                // === 3. Execute paged query (make sure your repository includes Farmer if needed)
                var (items, totalCount) = await _unitOfWork.PlotRepository
                    .GetAllPlotPagedAsync(
                        pageNumber: request.PageNumber,
                        pageSize: request.PageSize,
                        predicate: predicate,
                        cancellationToken: cancellationToken);

                var plotDTOs = _mapper.Map<IEnumerable<PlotDTO>>(items);

                return PagedResult<IEnumerable<PlotDTO>>.Success(
                    data: plotDTOs,
                    currentPage: request.PageNumber,
                    pageSize: request.PageSize,
                    totalCount: totalCount,
                    message: "Plots retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve plots. Request: {@Request}", request);
                return PagedResult<IEnumerable<PlotDTO>>.Failure(
                    error: "An error occurred while fetching plots.",
                    message: "Failed to retrieve plots");
            }
        }
    }

    // Helper extension to combine predicates cleanly
    public static class ExpressionExtensions
    {
        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T));

            var body = Expression.AndAlso(
                Expression.Invoke(left, parameter),
                Expression.Invoke(right, parameter));

            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }
    }
}