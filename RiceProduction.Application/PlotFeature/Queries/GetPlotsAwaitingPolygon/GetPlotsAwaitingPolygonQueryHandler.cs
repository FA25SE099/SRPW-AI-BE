using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsAwaitingPolygon;

public class GetPlotsAwaitingPolygonQueryHandler 
    : IRequestHandler<GetPlotsAwaitingPolygonQuery, PagedResult<IEnumerable<PlotAwaitingPolygonDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlotsAwaitingPolygonQueryHandler> _logger;

    public GetPlotsAwaitingPolygonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPlotsAwaitingPolygonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<IEnumerable<PlotAwaitingPolygonDto>>> Handle(
        GetPlotsAwaitingPolygonQuery request, 
        CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        try
        {
            _logger.LogInformation(
                "Fetching plots awaiting polygon - Page: {PageNumber}, PageSize: {PageSize}, ClusterManagerId: {ClusterManagerId}",
                request.PageNumber, request.PageSize, request.ClusterManagerId);

            Expression<Func<Plot, bool>> predicate = p => 
                (p.Boundary == null || p.Status == PlotStatus.PendingPolygon) && 
                p.Status != PlotStatus.Inactive;

            if (request.ClusterManagerId.HasValue)
            {
                var clusterManager = await _unitOfWork.ClusterManagerRepository
                    .GetClusterManagerByIdAsync(request.ClusterManagerId.Value, cancellationToken);

                if (clusterManager == null)
                {
                    _logger.LogWarning("ClusterManager Id {ClusterManagerId} not found.", request.ClusterManagerId);
                    return PagedResult<IEnumerable<PlotAwaitingPolygonDto>>.Success(
                        data: Enumerable.Empty<PlotAwaitingPolygonDto>(),
                        currentPage: request.PageNumber,
                        pageSize: request.PageSize,
                        totalCount: 0,
                        message: "No cluster manager found with the provided ID.");
                }

                var targetClusterId = clusterManager.ClusterId;
                predicate = predicate.And(p => p.Farmer != null && p.Farmer.ClusterId == targetClusterId);
            }
            else if (request.ClusterId.HasValue)
            {
                predicate = predicate.And(p => p.Farmer != null && p.Farmer.ClusterId == request.ClusterId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var search = request.SearchTerm.Trim();
                predicate = predicate.And(p =>
                    (p.SoThua.HasValue && p.SoThua.Value.ToString().Contains(search)) ||
                    (p.SoTo.HasValue && p.SoTo.Value.ToString().Contains(search)) ||
                    (p.Farmer != null && p.Farmer.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
                );
            }

            var plotsQuery = _unitOfWork.Repository<Plot>()
                .GetQueryable()
                .Include(p => p.Farmer)
                .Where(predicate);

            var allTasksQuery = _unitOfWork.Repository<PlotPolygonTask>()
                .GetQueryable()
                .Include(t => t.AssignedToSupervisor)
                .Where(t => t.Status != "Completed" && t.Status != "Cancelled");

            var tasksToUpdate = await allTasksQuery
                .Where(t => t.Status != "Late" && 
                           (t.Status == "Pending" || t.Status == "InProgress") &&
                           (DateTime.UtcNow - t.AssignedAt).Days > 5)
                .ToListAsync(cancellationToken);

            if (tasksToUpdate.Any())
            {
                foreach (var task in tasksToUpdate)
                {
                    task.Status = "Late";
                }
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Updated {Count} polygon tasks to Late status", tasksToUpdate.Count);
            }

            var tasksQuery = _unitOfWork.Repository<PlotPolygonTask>()
                .GetQueryable()
                .Include(t => t.AssignedToSupervisor)
                .Where(t => t.Status != "Completed" && t.Status != "Cancelled");

            var combinedQuery = from plot in plotsQuery
                                join task in tasksQuery on plot.Id equals task.PlotId into taskGroup
                                from task in taskGroup.DefaultIfEmpty()
                                select new { Plot = plot, Task = task };

            if (request.SupervisorId.HasValue)
            {
                combinedQuery = combinedQuery.Where(x => x.Task != null && x.Task.AssignedToSupervisorId == request.SupervisorId.Value);
            }

            if (request.HasActiveTask.HasValue)
            {
                if (request.HasActiveTask.Value)
                {
                    combinedQuery = combinedQuery.Where(x => x.Task != null);
                }
                else
                {
                    combinedQuery = combinedQuery.Where(x => x.Task == null);
                }
            }

            if (!string.IsNullOrWhiteSpace(request.TaskStatus))
            {
                combinedQuery = combinedQuery.Where(x => x.Task != null && x.Task.Status == request.TaskStatus);
            }

            var totalCount = await combinedQuery.CountAsync(cancellationToken);

            combinedQuery = ApplySorting(combinedQuery, request.SortBy, request.Descending);

            var pagedData = await combinedQuery
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var dtos = pagedData.Select(item =>
            {
                var plot = item.Plot;
                var task = item.Task;

                var daysWaiting = (DateTime.UtcNow - plot.CreatedAt).Days;

                int? taskDaysOverdue = task != null
                    ? (DateTime.UtcNow - task.AssignedAt).Days - 5
                    : null;

                // Fix negative values (not overdue yet)
                if (taskDaysOverdue.HasValue && taskDaysOverdue.Value < 0)
                {
                    taskDaysOverdue = null;
                }

                // ← RETURN MUST BE HERE, outside the if!
                return new PlotAwaitingPolygonDto
                {
                    PlotId = plot.Id,
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    Area = plot.Area,
                    Status = plot.Status.ToString(),
                    SoilType = plot.SoilType,

                    FarmerId = plot.FarmerId,
                    FarmerName = plot.Farmer?.FullName,
                    FarmerPhone = plot.Farmer?.PhoneNumber,
                    FarmerAddress = plot.Farmer?.Address,

                    CreatedAt = plot.CreatedAt.DateTime,
                    DaysAwaitingPolygon = daysWaiting,

                    HasActiveTask = task != null,
                    TaskId = task?.Id,
                    AssignedToSupervisorId = task?.AssignedToSupervisorId,
                    AssignedToSupervisorName = task?.AssignedToSupervisor?.FullName,
                    TaskStatus = task?.Status,
                    TaskAssignedAt = task?.AssignedAt,
                    TaskPriority = task?.Priority,
                    TaskDaysOverdue = taskDaysOverdue
                };
            }).ToList();
            return PagedResult<IEnumerable<PlotAwaitingPolygonDto>>.Success(
                data: dtos,
                currentPage: request.PageNumber,
                pageSize: request.PageSize,
                totalCount: totalCount,
                message: "Plots awaiting polygon retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve plots awaiting polygon. Request: {@Request}", request);
            return PagedResult<IEnumerable<PlotAwaitingPolygonDto>>.Failure(
                error: "An error occurred while fetching plots awaiting polygon.",
                message: "Failed to retrieve plots");
        }
    }

    private IQueryable<T> ApplySorting<T>(IQueryable<T> query, string? sortBy, bool descending) where T : class
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            sortBy = "DaysWaiting";

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression propertyAccess;

        switch (sortBy.ToLowerInvariant())
        {
            case "dayswaiting":
                var plotProperty = Expression.Property(parameter, "Plot");
                var createdProperty = Expression.Property(plotProperty, "CreatedAt");
                propertyAccess = createdProperty;
                descending = !descending;
                break;

            case "priority":
                var taskProperty = Expression.Property(parameter, "Task");
                var priorityProperty = Expression.Property(taskProperty, "Priority");
                propertyAccess = Expression.Condition(
                    Expression.Equal(taskProperty, Expression.Constant(null)),
                    Expression.Constant(0, typeof(int)),
                    priorityProperty
                );
                break;

            case "farmername":
                var plotProp = Expression.Property(parameter, "Plot");
                var farmerProp = Expression.Property(plotProp, "Farmer");
                var nameProp = Expression.Property(farmerProp, "FullName");
                propertyAccess = nameProp;
                break;

            case "area":
                var plotAreaProp = Expression.Property(parameter, "Plot");
                propertyAccess = Expression.Property(plotAreaProp, "Area");
                break;

            default:
                var defaultPlotProp = Expression.Property(parameter, "Plot");
                propertyAccess = Expression.Property(defaultPlotProp, "CreatedAt");
                descending = !descending;
                break;
        }

        var lambda = Expression.Lambda(propertyAccess, parameter);
        var methodName = descending ? "OrderByDescending" : "OrderBy";
        
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            new Type[] { typeof(T), propertyAccess.Type },
            query.Expression,
            Expression.Quote(lambda)
        );

        return query.Provider.CreateQuery<T>(resultExpression);
    }
}

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

