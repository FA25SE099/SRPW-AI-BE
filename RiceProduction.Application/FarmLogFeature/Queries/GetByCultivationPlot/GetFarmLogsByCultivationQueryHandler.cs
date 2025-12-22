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

            // Get all CultivationTasks for this PlotCultivation (across all versions)
            var cultivationTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.PlotCultivationId == request.PlotCultivationId,
                includeProperties: q => q.Include(ct => ct.ProductionPlanTask)
            );

            if (!cultivationTasks.Any())
            {
                return PagedResult<List<FarmLogDetailResponse>>.Success(
                    new List<FarmLogDetailResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No cultivation tasks found for this plot cultivation.");
            }

            var cultivationTaskIds = cultivationTasks.Select(ct => ct.Id).ToList();
            
            // Get all farm logs for all cultivation tasks (across all versions)
            var allLogs = await _unitOfWork.Repository<FarmLog>().ListAsync(
                filter: fl => cultivationTaskIds.Contains(fl.CultivationTaskId),
                orderBy: q => q.OrderByDescending(fl => fl.LoggedDate),
                includeProperties: q => q
                    .Include(fl => fl.CultivationTask)
                        .ThenInclude(ct => ct.ProductionPlanTask)
                    .Include(fl => fl.FarmLogMaterials)
                        .ThenInclude(flm => flm.Material)
            );

            var totalCount = allLogs.Count;
            
            // Apply pagination
            var pagedLogs = allLogs
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Map to response - group by ProductionPlanTask to show task continuity across versions
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
                InterruptionReason = fl.InterruptionReason,
                
                CultivationTaskName = fl.CultivationTask?.CultivationTaskName 
                    ?? fl.CultivationTask?.ProductionPlanTask?.TaskName 
                    ?? "Unknown Task",
                SoThua = plot.SoThua,
                SoTo = plot.SoTo,
                
                MaterialsUsed = fl.FarmLogMaterials.Select(flm => new FarmLogMaterialRecord
                {
                    MaterialName = flm.Material.Name,
                    ActualQuantityUsed = flm.ActualQuantityUsed,
                    ActualCost = flm.ActualCost,
                    Notes = flm.Notes
                }).ToList()
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} farm logs for Plot Cultivation {PCId} across {TaskCount} cultivation tasks",
                responseData.Count,
                request.PlotCultivationId,
                cultivationTasks.Count);

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