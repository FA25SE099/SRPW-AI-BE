using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;

namespace RiceProduction.Application.UAVFeature.Queries.GetVendorServiceOrders;

public class GetVendorServiceOrdersQueryHandler : IRequestHandler<GetVendorServiceOrdersQuery, PagedResult<List<UavServiceOrderResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetVendorServiceOrdersQueryHandler> _logger;

    public GetVendorServiceOrdersQueryHandler(IApplicationDbContext context, ILogger<GetVendorServiceOrdersQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<UavServiceOrderResponse>>> Handle(GetVendorServiceOrdersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var defaultStatuses = new List<RiceProduction.Domain.Enums.TaskStatus> { RiceProduction.Domain.Enums.TaskStatus.PendingApproval, RiceProduction.Domain.Enums.TaskStatus.Approved, RiceProduction.Domain.Enums.TaskStatus.InProgress, RiceProduction.Domain.Enums.TaskStatus.OnHold, RiceProduction.Domain.Enums.TaskStatus.Completed };
            var statusesToFilter = request.StatusFilter == null || !request.StatusFilter.Any() ? defaultStatuses : request.StatusFilter;

            var query = _context.UavServiceOrders
                .AsNoTracking()
                .Where(order => order.UavVendorId == request.VendorId &&
                                statusesToFilter.Contains(order.Status));

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedOrders = await query
                .Include(o => o.Group)
                    .ThenInclude(g => g.Cluster)
                .Include(o => o.Creator)
                .OrderBy(o => o.ScheduledDate)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

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