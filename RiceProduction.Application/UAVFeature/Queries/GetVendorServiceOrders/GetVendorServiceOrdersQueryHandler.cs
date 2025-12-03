using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq;
using System.Linq.Expressions;
using System;
using RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;

namespace RiceProduction.Application.UAVFeature.Queries.GetVendorServiceOrders;

public class GetVendorServiceOrdersQueryHandler : IRequestHandler<GetVendorServiceOrdersQuery, PagedResult<List<UavServiceOrderResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetVendorServiceOrdersQueryHandler> _logger;

    public GetVendorServiceOrdersQueryHandler(IUnitOfWork unitOfWork, ILogger<GetVendorServiceOrdersQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<UavServiceOrderResponse>>> Handle(GetVendorServiceOrdersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var defaultStatuses = new List<RiceProduction.Domain.Enums.TaskStatus> { RiceProduction.Domain.Enums.TaskStatus.PendingApproval, RiceProduction.Domain.Enums.TaskStatus.Approved, RiceProduction.Domain.Enums.TaskStatus.InProgress, RiceProduction.Domain.Enums.TaskStatus.OnHold, RiceProduction.Domain.Enums.TaskStatus.Completed };
            var statusesToFilter = request.StatusFilter == null || !request.StatusFilter.Any() ? defaultStatuses : request.StatusFilter;

            Expression<Func<UavServiceOrder, bool>> filter = order =>
                order.UavVendorId == request.VendorId &&
                statusesToFilter.Contains(order.Status);

            // 1. Tải toàn bộ dữ liệu phù hợp với filter
            var allOrders = await _unitOfWork.Repository<UavServiceOrder>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderBy(o => o.ScheduledDate),
                includeProperties: q => q
                    .Include(o => o.Group).ThenInclude(g => g.Cluster)
                    .Include(o => o.Creator)
            );

            // 2. Lấy tổng số lượng và áp dụng phân trang
            var totalCount = allOrders.Count;
            var pagedOrders = allOrders
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // 3. Ánh xạ dữ liệu
            var responseData = pagedOrders.Select(o => new UavServiceOrderResponse
            {
                OrderId = o.Id,
                OrderName = o.OrderName,
                Status = o.Status,
                Priority = o.Priority,
                ScheduledDate = o.ScheduledDate,
                ScheduledTime = o.ScheduledTime,
                GroupId = o.GroupId,
                GroupName = o.Group.Cluster.ClusterName,
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
                "Successfully retrieved service orders.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service orders for Vendor {VendorId}", request.VendorId);
            return PagedResult<List<UavServiceOrderResponse>>.Failure("Failed to retrieve service orders.", "GetOrdersFailed");
        }
    }
}