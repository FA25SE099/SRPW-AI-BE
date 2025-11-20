using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetCultivationTasksByPlan;

public class GetCultivationTasksByPlanQueryHandler : IRequestHandler<GetCultivationTasksByPlanQuery, Result<List<CultivationTaskSummaryResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetCultivationTasksByPlanQueryHandler> _logger;

    public GetCultivationTasksByPlanQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetCultivationTasksByPlanQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<CultivationTaskSummaryResponse>>> Handle(GetCultivationTasksByPlanQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.ProductionPlanTask.ProductionStage.ProductionPlanId == request.ProductionPlanId
                           && (!request.StatusFilter.HasValue || ct.Status == request.StatusFilter)
                           && (!request.PlotFilter.HasValue || ct.PlotCultivation.PlotId == request.PlotFilter),
                orderBy: q => q.OrderBy(ct => ct.ScheduledEndDate).ThenBy(ct => ct.ExecutionOrder),
                includeProperties: q => q
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionStage)
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.Plot)
                            .ThenInclude(p => p.Farmer)
            );

            var response = tasks.Select(ct => new CultivationTaskSummaryResponse
            {
                TaskId = ct.Id,
                TaskName = ct.CultivationTaskName ?? ct.ProductionPlanTask.TaskName,
                Description = ct.Description ?? ct.ProductionPlanTask.Description,
                TaskType = ct.TaskType ?? ct.ProductionPlanTask.TaskType,
                Status = ct.Status,
                
                ScheduledEndDate = ct.ScheduledEndDate,
                ActualStartDate = ct.ActualStartDate,
                ActualEndDate = ct.ActualEndDate,
                
                PlotId = ct.PlotCultivation.PlotId,
                PlotName = $"{ct.PlotCultivation.Plot.SoThua}/{ct.PlotCultivation.Plot.SoTo}",
                FarmerId = ct.PlotCultivation.Plot.FarmerId,
                FarmerName = ct.PlotCultivation.Plot.Farmer?.FullName ?? "Unknown",
                
                ActualMaterialCost = ct.ActualMaterialCost,
                ActualServiceCost = ct.ActualServiceCost,
                
                IsContingency = ct.IsContingency,
                ContingencyReason = ct.ContingencyReason
            }).ToList();

            _logger.LogInformation("Successfully retrieved {Count} cultivation tasks for plan {PlanId}", response.Count, request.ProductionPlanId);

            return Result<List<CultivationTaskSummaryResponse>>.Success(response, $"Successfully retrieved {response.Count} cultivation tasks.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cultivation tasks for plan {PlanId}", request.ProductionPlanId);
            return Result<List<CultivationTaskSummaryResponse>>.Failure("An error occurred while retrieving cultivation tasks.", "GetCultivationTasksFailed");
        }
    }
}

