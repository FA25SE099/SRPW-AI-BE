using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetByPlotId;

public class GetCultivationsForPlotQueryHandler : IRequestHandler<GetCultivationsForPlotQuery, PagedResult<List<PlotCultivationHistoryResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetCultivationsForPlotQueryHandler> _logger;

    public GetCultivationsForPlotQueryHandler(IUnitOfWork unitOfWork, ILogger<GetCultivationsForPlotQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<PlotCultivationHistoryResponse>>> Handle(GetCultivationsForPlotQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Xác thực Nông dân sở hữu Thửa đất
            var plotOwner = await _unitOfWork.Repository<Plot>().FindAsync(p => p.Id == request.PlotId && p.FarmerId == request.FarmerId);
            if (plotOwner == null)
            {
                _logger.LogWarning("Farmer {FarmerId} attempted to access unauthorized plot {PlotId}", request.FarmerId, request.PlotId);
                return PagedResult<List<PlotCultivationHistoryResponse>>.Failure("Plot not found or unauthorized.", "Unauthorized");
            }

            // 2. Xây dựng biểu thức lọc (Filter Expression)
            Expression<Func<PlotCultivation, bool>> filter = pc => pc.PlotId == request.PlotId;

            // 3. Định nghĩa các Includes
            Func<IQueryable<PlotCultivation>, IIncludableQueryable<PlotCultivation, object>> includes =
                q => q.Include(pc => pc.Season)
                      .Include(pc => pc.RiceVariety)
                      // Tải các CultivationTasks và ProductionPlan liên quan
                      .Include(pc => pc.CultivationTasks)
                          .ThenInclude(ct => ct.ProductionPlanTask)
                          .ThenInclude(ppt => ppt.ProductionStage)
                          .ThenInclude(ps => ps.ProductionPlan);

            // 4. Tải tất cả dữ liệu phù hợp (cho in-memory paging)
            var allCultivations = await _unitOfWork.Repository<PlotCultivation>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(pc => pc.PlantingDate),
                includeProperties: includes
            );

            // 5. Lấy tổng số lượng
            var totalCount = allCultivations.Count;

            // 6. Áp dụng Paging (in-memory)
            var pagedCultivations = allCultivations
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // 7. Ánh xạ sang Response DTO
            var responseData = pagedCultivations.Select(pc =>
            {
                // Tìm ProductionPlanName từ CultivationTask đầu tiên (giả định tất cả tasks thuộc 1 plan)
                var planName = pc.CultivationTasks
                    .Select(ct => ct.ProductionPlanTask?.ProductionStage?.ProductionPlan?.PlanName)
                    .FirstOrDefault(name => name != null);

                return new PlotCultivationHistoryResponse
                {
                    PlotCultivationId = pc.Id,
                    SeasonId = pc.SeasonId,
                    SeasonName = pc.Season.SeasonName, 
                    RiceVarietyId = pc.RiceVarietyId,
                    RiceVarietyName = pc.RiceVariety.VarietyName,
                    PlantingDate = pc.PlantingDate,
                    Area = pc.Area,
                    Status = pc.Status,
                    ActualYield = pc.ActualYield,
                    ProductionPlanName = planName ?? "N/A"
                };
            }).ToList();

            return PagedResult<List<PlotCultivationHistoryResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                $"Successfully retrieved {responseData.Count} cultivation records for plot {request.PlotId}."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cultivation history for plot {PlotId}", request.PlotId);
            return PagedResult<List<PlotCultivationHistoryResponse>>.Failure($"Failed to retrieve cultivation history: {ex.Message}");
        }
    }
}