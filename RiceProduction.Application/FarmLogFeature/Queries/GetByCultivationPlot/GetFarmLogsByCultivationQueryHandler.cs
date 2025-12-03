using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
using System.Linq;
using System.Collections.Generic;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;

public class GetFarmLogsByCultivationQueryHandler : IRequestHandler<GetFarmLogsByCultivationQuery, PagedResult<List<FarmLogDetailResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetFarmLogsByCultivationQueryHandler> _logger;

    public GetFarmLogsByCultivationQueryHandler(IUnitOfWork unitOfWork, ILogger<GetFarmLogsByCultivationQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmLogDetailResponse>>> Handle(GetFarmLogsByCultivationQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var pcRepo = _unitOfWork.Repository<PlotCultivation>();
            var plotCultivation = await pcRepo.FindAsync(
                pc => pc.Id == request.PlotCultivationId ,
                includeProperties: q => q.Include(pc => pc.Plot)
            );

            if (plotCultivation == null)
            {
                return PagedResult<List<FarmLogDetailResponse>>.Failure("Plot Cultivation not found or unauthorized.", "Unauthorized");
            }

            // 2. Xây dựng biểu thức lọc Farm Logs
            Expression<Func<FarmLog, bool>> filter = fl => fl.PlotCultivationId == request.PlotCultivationId;
            
            // 3. Tải toàn bộ Farm Logs (và các quan hệ cần thiết)
            var allLogs = await _unitOfWork.Repository<FarmLog>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(fl => fl.LoggedDate),
                includeProperties: q => q
                    .Include(fl => fl.CultivationTask)
                    .Include(fl => fl.FarmLogMaterials) // Vật tư chi tiết
                        .ThenInclude(flm => flm.Material)
            );

            var totalCount = allLogs.Count;
            
            // 4. Áp dụng Phân trang
            var pagedLogs = allLogs
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // 5. Ánh xạ dữ liệu
            var plot = plotCultivation.Plot;
            
            var responseData = pagedLogs.Select(fl => new FarmLogDetailResponse
            {
                FarmLogId = fl.Id,
                LoggedDate = fl.LoggedDate,
                WorkDescription = fl.WorkDescription,
                CompletionPercentage = fl.CompletionPercentage,
                ActualAreaCovered = fl.ActualAreaCovered,
                ServiceCost = fl.ServiceCost,
                ServiceNotes = fl.ServiceNotes,
                PhotoUrls = fl.PhotoUrls,
                WeatherConditions = fl.WeatherConditions,
                
                CultivationTaskName = fl.CultivationTask?.CultivationTaskName ?? fl.CultivationTask.ProductionPlanTask.TaskName,
                PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                
                MaterialsUsed = fl.FarmLogMaterials.Select(flm => new FarmLogMaterialRecord
                {
                    MaterialName = flm.Material.Name,
                    ActualQuantityUsed = flm.ActualQuantityUsed,
                    ActualCost = flm.ActualCost
                }).ToList()
            }).ToList();

            return PagedResult<List<FarmLogDetailResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved farm log history.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving farm log history for Plot Cultivation {PCId}", request.PlotCultivationId);
            return PagedResult<List<FarmLogDetailResponse>>.Failure("An error occurred while retrieving log history.", "GetFarmLogsFailed");
        }
    }
}