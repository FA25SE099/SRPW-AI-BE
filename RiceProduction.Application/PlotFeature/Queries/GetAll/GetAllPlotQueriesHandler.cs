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
                        (p.Farmer != null && !string.IsNullOrEmpty(p.Farmer.FullName) && p.Farmer.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    );
                }

                // === 3. Execute paged query (make sure your repository includes Farmer if needed)
                var (items, totalCount) = await _unitOfWork.PlotRepository
                    .GetAllPlotPagedAsync(
                        pageNumber: request.PageNumber,
                        pageSize: request.PageSize,
                        predicate: predicate,
                        cancellationToken: cancellationToken);

                var plotDTOs = _mapper.Map<List<PlotDTO>>(items);

                await EnrichWithEditabilityAsync(plotDTOs, cancellationToken);

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

        private async Task EnrichWithEditabilityAsync(List<PlotDTO> plotDTOs, CancellationToken cancellationToken)
        {
            try
            {
                var plotsWithPolygon = plotDTOs.Where(p => !string.IsNullOrEmpty(p.BoundaryGeoJson)).ToList();

                if (!plotsWithPolygon.Any())
                {
                    foreach (var plot in plotDTOs)
                    {
                        plot.IsEditableInCurrentSeason = true;
                        plot.EditabilityNote = "No polygon assigned yet";
                    }
                    return;
                }

                var farmerIds = plotDTOs.Select(p => p.FarmerId).Distinct().ToList();
                var farmers = await _unitOfWork.FarmerRepository.FindAsync(f => farmerIds.Contains(f.Id), cancellationToken);
                var farmerClusterMap = farmers.ToDictionary(f => f.Id, f => f.ClusterId);

                var (currentSeason, currentYear) = await GetCurrentSeasonAndYearAsync();

                if (currentSeason == null)
                {
                    foreach (var plot in plotDTOs)
                    {
                        plot.IsEditableInCurrentSeason = true;
                        plot.EditabilityNote = "No active season";
                    }
                    return;
                }

                var clusterIds = farmerClusterMap.Values.Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();
                
                if (!clusterIds.Any())
                {
                    foreach (var plot in plotDTOs)
                    {
                        plot.IsEditableInCurrentSeason = true;
                        plot.EditabilityNote = "Farmer not assigned to cluster";
                    }
                    return;
                }

                var yearSeasons = await _unitOfWork.Repository<YearSeason>()
                    .ListAsync(ys => clusterIds.Contains(ys.ClusterId) 
                              && ys.SeasonId == currentSeason.Id 
                              && ys.Year == currentYear);

                var clusterYearSeasonMap = yearSeasons.ToDictionary(ys => ys.ClusterId, ys => ys.Id);

                foreach (var plot in plotDTOs)
                {
                    if (string.IsNullOrEmpty(plot.BoundaryGeoJson))
                    {
                        plot.IsEditableInCurrentSeason = true;
                        plot.EditabilityNote = "No polygon assigned yet";
                        continue;
                    }

                    var clusterId = farmerClusterMap.GetValueOrDefault(plot.FarmerId);
                    if (!clusterId.HasValue)
                    {
                        plot.IsEditableInCurrentSeason = true;
                        plot.EditabilityNote = "Farmer not assigned to cluster";
                        continue;
                    }

                    var yearSeasonId = clusterYearSeasonMap.GetValueOrDefault(clusterId.Value);
                    if (yearSeasonId == Guid.Empty)
                    {
                        plot.IsEditableInCurrentSeason = true;
                        plot.EditabilityNote = "No active year-season for cluster";
                        continue;
                    }

                    var isInGroup = await _unitOfWork.PlotRepository
                        .IsPlotAssignedToGroupForYearSeasonAsync(plot.PlotId, yearSeasonId, cancellationToken);

                    plot.IsEditableInCurrentSeason = !isInGroup;
                    plot.EditabilityNote = isInGroup 
                        ? $"Assigned to group in current season ({currentSeason.SeasonName} {currentYear})"
                        : $"Editable in current season ({currentSeason.SeasonName} {currentYear})";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich plots with editability info. Skipping.");
            }
        }

        private async Task<(Season? season, int year)> GetCurrentSeasonAndYearAsync()
        {
            var today = DateTime.Now;
            var currentMonth = today.Month;
            var currentDay = today.Day;

            var allSeasons = await _unitOfWork.Repository<Season>().ListAllAsync();

            foreach (var season in allSeasons)
            {
                if (IsDateInSeasonRange(currentMonth, currentDay, season.StartDate, season.EndDate))
                {
                    // Parse DD/MM format
                    var startParts = season.StartDate.Split('/');
                    int startMonth = int.Parse(startParts[1]);

                    int year = today.Year;
                    if (currentMonth < startMonth && startMonth > 6)
                    {
                        year--;
                    }

                    return (season, year);
                }
            }

            return (null, today.Year);
        }

        private bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr)
        {
            try
            {
                var startParts = startDateStr.Split('/');
                var endParts = endDateStr.Split('/');

                // Parse DD/MM format
                int startDay = int.Parse(startParts[0]);
                int startMonth = int.Parse(startParts[1]);
                int endDay = int.Parse(endParts[0]);
                int endMonth = int.Parse(endParts[1]);

                int currentDate = month * 100 + day;
                int seasonStart = startMonth * 100 + startDay;
                int seasonEnd = endMonth * 100 + endDay;

                if (seasonStart > seasonEnd)
                {
                    return currentDate >= seasonStart || currentDate <= seasonEnd;
                }
                else
                {
                    return currentDate >= seasonStart && currentDate <= seasonEnd;
                }
            }
            catch
            {
                return false;
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