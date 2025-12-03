using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq;
using System.Linq.Expressions;

namespace RiceProduction.Application.GroupFeature.Queries.GetGroupByClusterManager;

public class GetGroupsByManagerQueryHandler : IRequestHandler<GetGroupsByManagerQuery, PagedResult<List<ClusterManagerGroupResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetGroupsByManagerQueryHandler> _logger;

    public GetGroupsByManagerQueryHandler(IUnitOfWork unitOfWork, ILogger<GetGroupsByManagerQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<ClusterManagerGroupResponse>>> Handle(GetGroupsByManagerQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Tải Cluster ID mà Manager này quản lý
            var managedCluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.ClusterManagerId == request.ClusterManagerId);

            if (managedCluster == null)
            {
                return PagedResult<List<ClusterManagerGroupResponse>>.Failure("No cluster found managed by this user.", "ClusterNotFound");
            }
            
            // 2. Xây dựng Filter Expression
            Expression<Func<Group, bool>> filter = g =>
                g.ClusterId == managedCluster.Id &&
                (!request.StatusFilter.HasValue || g.Status == request.StatusFilter.Value);

            // 3. Tải tất cả Groups phù hợp với Includes cần thiết
            var allGroups = await _unitOfWork.Repository<Group>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderBy(g => g.PlantingDate),
                includeProperties: q => q
                    .Include(g => g.RiceVariety)
                    .Include(g => g.Supervisor)
                    .Include(g => g.Plots) // Để đếm Plots
                    .Include(g => g.ProductionPlans.Where(pp => pp.Status != RiceProduction.Domain.Enums.TaskStatus.Cancelled)) // Để đếm Active Plans
            );

            var totalCount = allGroups.Count;

            // 4. Áp dụng Paging và Ánh xạ
            var pagedGroups = allGroups
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var responseData = pagedGroups.Select(g => new ClusterManagerGroupResponse
            {
                GroupId = g.Id,
                Status = g.Status,
                TotalArea = g.TotalArea,
                // Tạo tên Group mô phỏng
                GroupName = $"{managedCluster.ClusterName} / Group {g.Id.ToString().Substring(0, 4)}", 
                PlantingDate = g.PlantingDate,
                RiceVarietyName = g.RiceVariety?.VarietyName ?? "N/A",
                SupervisorName = g.Supervisor?.FullName ?? "Chưa phân công",
                TotalPlots = g.Plots.Count,
                ActivePlans = g.ProductionPlans.Count
            }).ToList();

            return PagedResult<List<ClusterManagerGroupResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved managed groups."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups managed by Cluster Manager {ManagerId}", request.ClusterManagerId);
            return PagedResult<List<ClusterManagerGroupResponse>>.Failure("Failed to retrieve managed groups.", "GetManagedGroupsFailed");
        }
    }
}