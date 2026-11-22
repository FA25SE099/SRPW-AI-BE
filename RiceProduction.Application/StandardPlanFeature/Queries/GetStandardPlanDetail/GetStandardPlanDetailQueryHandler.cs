using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.StandardPlanFeature.Queries.GetStandardPlanDetail;

public class GetStandardPlanDetailQueryHandler : 
    IRequestHandler<GetStandardPlanDetailQuery, Result<StandardPlanDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetStandardPlanDetailQueryHandler> _logger;

    public GetStandardPlanDetailQueryHandler(
        IApplicationDbContext context,
        ILogger<GetStandardPlanDetailQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<StandardPlanDetailDto>> Handle(
        GetStandardPlanDetailQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting standard plan detail for ID: {StandardPlanId}", request.StandardPlanId);

            var standardPlan = await _context.StandardPlans
                .AsNoTracking()
                .Include(sp => sp.Category)
                .Include(sp => sp.Creator)
                .Include(sp => sp.StandardPlanStages.OrderBy(s => s.SequenceOrder))
                    .ThenInclude(stage => stage.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                        .ThenInclude(task => task.StandardPlanTaskMaterials)
                            .ThenInclude(material => material.Material)
                .AsSplitQuery()
                .FirstOrDefaultAsync(sp => sp.Id == request.StandardPlanId, cancellationToken);

            if (standardPlan == null)
            {
                _logger.LogWarning("Standard plan with ID {StandardPlanId} not found", request.StandardPlanId);
                return Result<StandardPlanDetailDto>.Failure(
                    $"Standard plan with ID {request.StandardPlanId} not found.",
                    "StandardPlanNotFound");
            }

            var response = new StandardPlanDetailDto
            {
                Id = standardPlan.Id,
                PlanName = standardPlan.PlanName,
                Description = standardPlan.Description,
                TotalDurationDays = standardPlan.TotalDurationDays,
                IsActive = standardPlan.IsActive,
                
                CategoryId = standardPlan.CategoryId,
                CategoryName = standardPlan.Category.CategoryName,
                
                CreatedBy = standardPlan.CreatedBy,
                CreatorName = standardPlan.Creator?.FullName ?? "N/A",
                CreatedAt = standardPlan.CreatedAt,
                LastModified = standardPlan.LastModified,
                
                Stages = standardPlan.StandardPlanStages
                    .OrderBy(s => s.SequenceOrder)
                    .Select(stage => new StandardPlanStageDetailDto
                    {
                        Id = stage.Id,
                        StageName = stage.StageName,
                        SequenceOrder = stage.SequenceOrder,
                        ExpectedDurationDays = stage.ExpectedDurationDays,
                        IsMandatory = stage.IsMandatory,
                        Notes = stage.Notes,
                        
                        Tasks = stage.StandardPlanTasks
                            .OrderBy(t => t.SequenceOrder)
                            .Select(task => new StandardPlanTaskDetailDto
                            {
                                Id = task.Id,
                                TaskName = task.TaskName,
                                Description = task.Description,
                                TaskType = task.TaskType,
                                Priority = task.Priority,
                                DaysAfter = task.DaysAfter,
                                DurationDays = task.DurationDays,
                                SequenceOrder = task.SequenceOrder,
                                
                                Materials = task.StandardPlanTaskMaterials
                                    .Select(material => new StandardPlanTaskMaterialDetailDto
                                    {
                                        Id = material.Id,
                                        MaterialId = material.MaterialId,
                                        MaterialName = material.Material.Name,
                                        MaterialType = material.Material.Type,
                                        MaterialUnit = material.Material.Unit,
                                        QuantityPerHa = material.QuantityPerHa
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList(),
                
                TotalStages = standardPlan.StandardPlanStages.Count,
                TotalTasks = standardPlan.StandardPlanStages
                    .SelectMany(s => s.StandardPlanTasks)
                    .Count(),
                TotalMaterialTypes = standardPlan.StandardPlanStages
                    .SelectMany(s => s.StandardPlanTasks)
                    .SelectMany(t => t.StandardPlanTaskMaterials)
                    .Select(m => m.MaterialId)
                    .Distinct()
                    .Count()
            };

            _logger.LogInformation(
                "Successfully retrieved standard plan detail. ID: {StandardPlanId}, Stages: {StageCount}, Tasks: {TaskCount}",
                response.Id, response.TotalStages, response.TotalTasks);

            return Result<StandardPlanDetailDto>.Success(
                response,
                "Successfully retrieved standard plan details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting standard plan detail for ID: {StandardPlanId}", request.StandardPlanId);
            return Result<StandardPlanDetailDto>.Failure(
                "Failed to retrieve standard plan details.",
                "GetStandardPlanDetailFailed");
        }
    }
}

