using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupResponse;
using RiceProduction.Domain.Entities;
using RiceProduction.Application.Common.Interfaces;
namespace RiceProduction.Application.GroupFeature.Queries.GetGroupDetail;

public class GetGroupDetailQueryHandler :
    IRequestHandler<GetGroupDetailQuery, Result<GroupDetailResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetGroupDetailQueryHandler> _logger;

    public GetGroupDetailQueryHandler(IUnitOfWork unitOfWork, ILogger<GetGroupDetailQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<GroupDetailResponse>> Handle(GetGroupDetailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Tải Group cùng tất cả các mối quan hệ lồng nhau
            var group = await _unitOfWork.Repository<Group>().FindAsync(
                match: g => g.Id == request.GroupId,
                includeProperties: q => q
                    .Include(g => g.Cluster)
                    .Include(g => g.Supervisor)
                    .Include(g => g.RiceVariety)
                    .Include(g => g.Plots).ThenInclude(p => p.Farmer) // Plots và Farmer
                    .Include(g => g.ProductionPlans).ThenInclude(pp => pp.StandardPlan) // Plans và Standard Plan
                    .Include(g => g.UavServiceOrders).ThenInclude(uav => uav.UavVendor) // UAV Orders và Vendor
                    .Include(g => g.Alerts)
            );

            if (group == null)
            {
                return Result<GroupDetailResponse>.Failure($"Group with ID {request.GroupId} not found.", "GroupNotFound");
            }

            // --- Ánh xạ Plots ---
            var plotsResponse = group.Plots.Select(p => new GroupPlotResponse
            {
                Id = p.Id,
                Area = p.Area,
                SoThua = p.SoThua,
                SoTo = p.SoTo,
                SoilType = p.SoilType,
                Status = p.Status,
                FarmerName = p.Farmer.FullName
            }).ToList();

            // --- Ánh xạ Production Plans ---
            var plansResponse = group.ProductionPlans.Select(p => new GroupProductionPlanResponse
            {
                Id = p.Id,
                PlanName = p.PlanName,
                BasePlantingDate = p.BasePlantingDate,
                Status = p.Status,
                TotalArea = p.TotalArea
            }).ToList();

            // --- Ánh xạ UAV Service Orders ---
            var uavOrdersResponse = group.UavServiceOrders.Select(u => new GroupUavOrderResponse
            {
                Id = u.Id,
                OrderName = u.OrderName,
                ScheduledDate = u.ScheduledDate,
                Status = u.Status,
                TotalArea = u.TotalArea,
                EstimatedCost = u.EstimatedCost,
                VendorName = u.UavVendor?.VendorName // Giả định UavVendor có VendorName
            }).ToList();

            // --- Ánh xạ Alerts ---
            var alertsResponse = group.Alerts.Select(a => new GroupAlertResponse
            {
                Id = a.Id,
                Title = a.Title,
                Severity = a.Severity,
                Status = a.Status,
                CreatedAt = a.CreatedAt.UtcDateTime
            }).ToList();

            // --- Ánh xạ Group chính ---
            var response = new GroupDetailResponse
            {
                Id = group.Id,
                ClusterName = group.Cluster.ClusterName,
                SeasonId = group.SeasonId,
                PlantingDate = group.PlantingDate,
                Status = group.Status,
                TotalArea = group.TotalArea,
                RiceVarietyName = group.RiceVariety?.VarietyName, // Giả định RiceVariety có VarietyName
                SupervisorName = group.Supervisor?.FullName, // Giả định Supervisor có FullName

                Plots = plotsResponse,
                ProductionPlans = plansResponse,
                UavServiceOrders = uavOrdersResponse,
                Alerts = alertsResponse
            };

            _logger.LogInformation("Successfully retrieved details for Group ID {GroupId}.", request.GroupId);

            return Result<GroupDetailResponse>.Success(response, "Successfully retrieved group details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group details for ID {GroupId}.", request.GroupId);

            return Result<GroupDetailResponse>.Failure("An error occurred while retrieving group details.", "GetGroupDetailFailed");
        }
    }
}