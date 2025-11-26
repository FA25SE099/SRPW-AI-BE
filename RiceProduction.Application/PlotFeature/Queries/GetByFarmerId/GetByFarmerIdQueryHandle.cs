using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
public class GetByFarmerIdQueryHandler : IRequestHandler<GetByFarmerIdQuery, PagedResult<List<PlotListResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetByFarmerIdQueryHandler> _logger;

    public GetByFarmerIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetByFarmerIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<PlotListResponse>>> Handle(GetByFarmerIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Xây dựng biểu thức lọc (Filter Expression)
            Expression<Func<Plot, bool>> filter = p =>
                p.FarmerId == request.FarmerId &&
                (!request.Status.HasValue || p.Status == request.Status.Value) &&
                (!request.IsUnassigned.HasValue ||
                    (request.IsUnassigned.Value && p.GroupId == null) || // Lọc thửa CHƯA gán
                    (!request.IsUnassigned.Value && p.GroupId != null)); // Lọc thửa ĐÃ gán

            // 2. Định nghĩa các Includes
            Func<IQueryable<Plot>, IIncludableQueryable<Plot, object>> includes =
                q => q.Include(p => p.Group)
                        .ThenInclude(g => g.Cluster) // Cần để lấy tên Cluster (GroupName)
                      .Include(p => p.Alerts) // Cần để đếm ActiveAlerts
                      .Include(p => p.PlotCultivations); // Cần để đếm ActiveCultivations

            // 3. Tải tất cả dữ liệu phù hợp (cho in-memory paging)
            var allPlots = await _unitOfWork.Repository<Plot>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderBy(p => p.SoTo).ThenBy(p => p.SoThua),
                includeProperties: includes
            );

            // 4. Lấy tổng số lượng
            var totalCount = allPlots.Count;

            // 5. Áp dụng Paging (in-memory)
            var pagedPlots = allPlots
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // 6. Ánh xạ sang Response DTO
            var responseData = pagedPlots.Select(p => new PlotListResponse
            {
                PlotId = p.Id,
                Area = p.Area,
                SoThua = p.SoThua,
                SoTo = p.SoTo,
                Status = p.Status,
                Boundary = p.Boundary != null ? p.Boundary.AsText() : null,
                Coordinate = p.Coordinate != null ? p.Coordinate.AsText() : null,
                GroupId = p.GroupId,
                GroupName = p.Group?.Cluster?.ClusterName, // Lấy tên Cluster làm tên Group
                ActiveAlerts = p.Alerts.Count(a => a.Status == AlertStatus.Pending || a.Status == AlertStatus.UnderReview),
                ActiveCultivations = p.PlotCultivations.Count(pc => pc.Status == CultivationStatus.Planned || pc.Status == CultivationStatus.InProgress)
            }).ToList();

            return PagedResult<List<PlotListResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                $"Successfully retrieved {responseData.Count} plots for farmer {request.FarmerId}."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving plots for farmer {FarmerId}", request.FarmerId);
            return PagedResult<List<PlotListResponse>>.Failure($"Failed to retrieve plots: {ex.Message}");
        }
    }
}