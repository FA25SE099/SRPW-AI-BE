using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace RiceProduction.Application.ProductionPlanFeature.Commands.CreateProductionPlan;

public class CreateProductionPlanCommandHandler : 
    IRequestHandler<CreateProductionPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateProductionPlanCommandHandler> _logger;

    public CreateProductionPlanCommandHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateProductionPlanCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateProductionPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            decimal effectiveTotalArea;

            // --- 1. Determine the effective TotalArea for the Plan ---
            if (request.GroupId.HasValue)
            {
                // Truy vấn Group để lấy TotalArea, sử dụng FindAsync vì GenericRepository không có GetByIdAsync(Guid)
                var group = await _unitOfWork.Repository<Group>().FindAsync(g => g.Id == request.GroupId.Value);

                if (group == null)
                {
                    return Result<Guid>.Failure($"Group with ID {request.GroupId.Value} not found.", "GroupNotFound");
                }
                
                if (group.TotalArea == null || group.TotalArea.Value <= 0)
                {
                    // Trả về lỗi nếu TotalArea trong Group không hợp lệ
                    return Result<Guid>.Failure("Group's Total Area is not defined or is zero.", "GroupAreaMissing");
                }
                
                // Sử dụng TotalArea từ Group entity
                effectiveTotalArea = group.TotalArea.Value;
                _logger.LogInformation("Using TotalArea from Group ID {GroupId}: {Area}", request.GroupId.Value, effectiveTotalArea);
            }
            else
            {
                // Sử dụng TotalArea được cung cấp trong Command (đã được Validator kiểm tra)
                effectiveTotalArea = request.TotalArea!.Value; 
                _logger.LogInformation("Using TotalArea provided in Command: {Area}", effectiveTotalArea);
            }
            
            // 2. Create the main ProductionPlan entity
            var plan = new ProductionPlan
            {
                GroupId = request.GroupId,
                StandardPlanId = request.StandardPlanId,
                PlanName = request.PlanName,
                BasePlantingDate = request.BasePlantingDate,
                Status = RiceProduction.Domain.Enums.TaskStatus.Draft,
                // Lưu diện tích đã xác định vào Plan entity
                TotalArea = effectiveTotalArea, 
            };

            // 3. Fetch Material Prices for Cost Calculation based on MaterialPrice entity
            var materialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            // Ngày tham chiếu để tìm giá (BasePlantingDate)
            var priceReferenceDate = request.BasePlantingDate.Date;

            // Truy vấn các mức giá có hiệu lực trước hoặc vào ngày tham chiếu
            var materialPrices = _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate
            ).Result.OrderByDescending(p => p.ValidFrom).FirstOrDefault();

            // Group theo MaterialId và chọn mức giá có ngày ValidFrom mới nhất
            // var materialPrices = potentialPrices
            //     .GroupBy(p => p.MaterialId)
            //     .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
            //     .ToDictionary(p => p.MaterialId, p => p.PricePerMaterial);
            
            // 4. Prepare lists for batch insertion
            var planStages = new List<ProductionStage>();
            var allPlanTasks = new List<ProductionPlanTask>();
            
            // 5. Map and calculate nested entities
            foreach (var stageDto in request.Stages.OrderBy(s => s.SequenceOrder))
            {
                // A. Create ProductionStage
                var stage = new ProductionStage
                {
                    ProductionPlan = plan, 
                    StageName = stageDto.StageName,
                    Description = stageDto.Description,
                    SequenceOrder = stageDto.SequenceOrder,
                    TypicalDurationDays = stageDto.TypicalDurationDays,
                    ColorCode = stageDto.ColorCode,
                    IsActive = true,
                };
                
                planStages.Add(stage);

                // B. Map Tasks within the current Stage
                foreach (var taskDto in stageDto.Tasks)
                {
                    var task = new ProductionPlanTask
                    {
                        ProductionStage = stage, 
                        TaskName = taskDto.TaskName,
                        Description = taskDto.Description,
                        TaskType = taskDto.TaskType,
                        ScheduledDate = taskDto.ScheduledDate,
                        ScheduledEndDate = taskDto.ScheduledEndDate,
                        Priority = taskDto.Priority,
                        SequenceOrder = taskDto.SequenceOrder,
                        Status = RiceProduction.Domain.Enums.TaskStatus.Draft,
                    };
                    
                    decimal totalTaskMaterialCost = 0;

                    // C. Map Materials for the current Task and CALCULATE
                    foreach (var materialDto in taskDto.Materials)
                    {
                        var material = new ProductionPlanTaskMaterial
                        {
                            MaterialId = materialDto.MaterialId,
                            QuantityPerHa = materialDto.QuantityPerHa,
                            ProductionPlanTask = task
                        };

                        // if (!materialPrices.TryGetValue(materialDto.MaterialId, out var unitPrice))
                        // {
                        //     _logger.LogWarning("Material with ID {MaterialId} not found in price list valid for {Date}. Using PricePerMaterial = 0.", materialDto.MaterialId, priceReferenceDate.ToShortDateString());
                        //     unitPrice = 0M;
                        // }
                        
                        // Calculation: EstimatedAmount = QuantityPerHa * effectiveTotalArea * PricePerMaterial
                        decimal totalQuantity = materialDto.QuantityPerHa / materialPrices.Material.AmmountPerMaterial.Value * effectiveTotalArea;
                        material.EstimatedAmount = totalQuantity * materialPrices.PricePerMaterial;
                        
                        totalTaskMaterialCost += material.EstimatedAmount.GetValueOrDefault(0);
                        
                        task.ProductionPlanTaskMaterials.Add(material);
                    }

                    task.EstimatedMaterialCost = totalTaskMaterialCost;
                    allPlanTasks.Add(task);
                }
            }
            
            // 6. Add entities to repositories for saving
            await _unitOfWork.Repository<ProductionPlan>().AddAsync(plan);
            await _unitOfWork.Repository<ProductionStage>().AddRangeAsync(planStages);
            await _unitOfWork.Repository<ProductionPlanTask>().AddRangeAsync(allPlanTasks);

            // 7. Commit transaction
            await _unitOfWork.Repository<ProductionPlan>().SaveChangesAsync();
            await _unitOfWork.Repository<ProductionStage>().SaveChangesAsync();
            await _unitOfWork.Repository<ProductionPlanTask>().SaveChangesAsync();

            _logger.LogInformation("Successfully created new ProductionPlan with ID {PlanId}, {StageCount} stages, and {TaskCount} tasks. Area used: {Area}", plan.Id, planStages.Count, allPlanTasks.Count, effectiveTotalArea);

            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' created successfully with total area {effectiveTotalArea} ha.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating production plan: {PlanName}", request.PlanName);

            return Result<Guid>.Failure("An error occurred while creating the production plan.", "CreateProductionPlan failed.");
        }
    }
}