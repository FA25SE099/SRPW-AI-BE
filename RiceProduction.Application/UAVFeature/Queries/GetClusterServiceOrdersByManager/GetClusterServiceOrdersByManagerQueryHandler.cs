using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;

namespace RiceProduction.Application.UAVFeature.Queries.GetClusterServiceOrdersByManager;

public class GetClusterServiceOrdersByManagerQueryHandler
    : IRequestHandler<GetClusterServiceOrdersByManagerQuery, PagedResult<List<UavServiceOrderResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetClusterServiceOrdersByManagerQueryHandler> _logger;

    public GetClusterServiceOrdersByManagerQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetClusterServiceOrdersByManagerQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<UavServiceOrderResponse>>> Handle(
        GetClusterServiceOrdersByManagerQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var clusterManager = await _unitOfWork.ClusterManagerRepository
                .GetClusterManagerByIdAsync(request.ClusterManagerId, cancellationToken);

            if (clusterManager == null)
            {
                return PagedResult<List<UavServiceOrderResponse>>.Failure(
                    $"Cluster Manager with ID {request.ClusterManagerId} not found",
                    "ClusterManagerNotFound");
            }

            if (clusterManager.ClusterId == null)
            {
                return PagedResult<List<UavServiceOrderResponse>>.Success(
                    new List<UavServiceOrderResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "Cluster Manager is not assigned to any cluster.");
            }

            var clusterId = clusterManager.ClusterId.Value;

            var defaultStatuses = new List<RiceProduction.Domain.Enums.TaskStatus>
            {
                RiceProduction.Domain.Enums.TaskStatus.PendingApproval,
                RiceProduction.Domain.Enums.TaskStatus.Approved,
                RiceProduction.Domain.Enums.TaskStatus.InProgress,
                RiceProduction.Domain.Enums.TaskStatus.OnHold,
                RiceProduction.Domain.Enums.TaskStatus.Completed
            };
            var statusesToFilter = request.StatusFilter == null || !request.StatusFilter.Any()
                ? defaultStatuses
                : request.StatusFilter;

            Expression<Func<UavServiceOrder, bool>> filter = order =>
                order.Group.ClusterId == clusterId &&
                statusesToFilter.Contains(order.Status);

            var allOrders = await _unitOfWork.Repository<UavServiceOrder>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(o => o.ScheduledDate),
                includeProperties: q => q
                    .Include(o => o.Group).ThenInclude(g => g.Cluster)
                    .Include(o => o.Creator)
                    .Include(o => o.UavVendor));

            var totalCount = allOrders.Count;
            var pagedOrders = allOrders
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var responseData = pagedOrders.Select(o => new UavServiceOrderResponse
            {
                OrderId = o.Id,
                OrderName = o.OrderName,
                Status = o.Status,
                Priority = o.Priority,
                ScheduledDate = o.ScheduledDate,
                ScheduledTime = o.ScheduledTime,
                GroupId = o.GroupId,
                GroupName = o.Group.GroupName ?? o.Group.Cluster.ClusterName,
                TotalArea = o.TotalArea,
                TotalPlots = o.TotalPlots,
                EstimatedCost = o.EstimatedCost,
                ActualCost = o.ActualCost,
                CompletionPercentage = o.CompletionPercentage,
                CreatorName = o.Creator?.FullName
            }).ToList();

            return PagedResult<List<UavServiceOrderResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved UAV orders for the cluster.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving UAV orders for Cluster Manager {ClusterManagerId}",
                request.ClusterManagerId);

            return PagedResult<List<UavServiceOrderResponse>>.Failure(
                "Failed to retrieve UAV orders.",
                "GetClusterOrdersFailed");
        }
    }
}


