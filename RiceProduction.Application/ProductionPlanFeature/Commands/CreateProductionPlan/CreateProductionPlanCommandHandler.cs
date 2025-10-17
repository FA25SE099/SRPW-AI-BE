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
using System.Threading.Tasks;

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
            var currentUtc = DateTime.UtcNow; // Sử dụng cho các trường Auditable

            // --- 1. Determine the effective TotalArea for the Plan ---
            if (request.GroupId.HasValue)
            {
                var group = await _unitOfWork.Repository<Group>().FindAsync(g => g.Id == request.GroupId.Value);

                if (group == null)
                {
                    return Result<Guid>.Failure($"Group with ID {request.GroupId.Value} not found.", "GroupNotFound");
                }
                
                if (group.TotalArea == null || group.TotalArea.Value <= 0)
                {
                    return Result<Guid>.Failure("Group's Total Area is not defined or is zero.", "GroupAreaMissing");
                }
                
                effectiveTotalArea = group.TotalArea.Value;
                _logger.LogInformation("Using TotalArea from Group ID {GroupId}: {Area}", request.GroupId.Value, effectiveTotalArea);
            }
            else
            {
                effectiveTotalArea = request.TotalArea!.Value; 
                _logger.LogInformation("Using TotalArea provided in Command: {Area}", effectiveTotalArea);
            }
            
            // --- FIX: Chuyển đổi BasePlantingDate sang UTC Kind để lưu DB an toàn ---
            var basePlantingDateUtc = DateTime.SpecifyKind(request.BasePlantingDate, DateTimeKind.Utc);

            // 2. Create the main ProductionPlan entity
            var plan = new ProductionPlan
            {
                GroupId = request.GroupId,
                StandardPlanId = request.StandardPlanId,
                PlanName = request.PlanName,
                BasePlantingDate = basePlantingDateUtc, // FIXED
                Status = RiceProduction.Domain.Enums.TaskStatus.Draft,
                TotalArea = effectiveTotalArea,
                // Các trường Auditable sẽ được tự động xử lý nếu BaseAuditableEntity đúng
                // Nếu không, cần gán: CreatedAt = currentUtc, LastModified = currentUtc
            };

            // --- 3. Fetch Material Prices for Cost Calculation (FIXED LOGIC) ---
            var materialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.BasePlantingDate.Date, DateTimeKind.Utc);

            // 3a. Truy vấn tất cả các mức giá có hiệu lực
            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate
            );

            // 3b. Group theo MaterialId và chọn mức giá có ngày ValidFrom mới nhất
            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p.PricePerMaterial);
            
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
                    // Nếu BaseAuditableEntity không tự set, cần set thủ công:
                    // CreatedAt = currentUtc, LastModified = currentUtc
                };
                
                planStages.Add(stage);

                // B. Map Tasks within the current Stage
                foreach (var taskDto in stageDto.Tasks)
                {
                    // FIX: Chuyển đổi ScheduledDate sang UTC Kind để lưu DB an toàn
                    var scheduledDateUtc = DateTime.SpecifyKind(taskDto.ScheduledDate, DateTimeKind.Utc);
                    var scheduledEndDateUtc = taskDto.ScheduledEndDate.HasValue 
                        ? DateTime.SpecifyKind(taskDto.ScheduledEndDate.Value, DateTimeKind.Utc) 
                        : (DateTime?)null;

                    var task = new ProductionPlanTask
                    {
                        ProductionStage = stage, 
                        TaskName = taskDto.TaskName,
                        Description = taskDto.Description,
                        TaskType = taskDto.TaskType,
                        ScheduledDate = scheduledDateUtc, // FIXED
                        ScheduledEndDate = scheduledEndDateUtc, // FIXED
                        Priority = taskDto.Priority,
                        SequenceOrder = taskDto.SequenceOrder,
                        Status = RiceProduction.Domain.Enums.TaskStatus.Draft,
                        // CreatedAt = currentUtc, LastModified = currentUtc
                    };
                    
                    decimal totalTaskMaterialCost = 0;

                    // C. Map Materials for the current Task and CALCULATE
                    foreach (var materialDto in taskDto.Materials)
                    {
                        // Truy vấn chi tiết Material (cần cho AmmountPerMaterial)
                        var materialDetail = await _unitOfWork.Repository<Material>().FindAsync(m => m.Id == materialDto.MaterialId);
                        
                        var material = new ProductionPlanTaskMaterial
                        {
                            MaterialId = materialDto.MaterialId,
                            QuantityPerHa = materialDto.QuantityPerHa,
                            ProductionPlanTask = task
                            // CreatedAt = currentUtc, LastModified = currentUtc
                        };
                        
                        decimal unitPrice = materialPriceMap.GetValueOrDefault(materialDto.MaterialId, 0M);
                        decimal estimatedAmount = 0M;

                        // Tính toán chi phí:
                        if (materialDetail != null && materialDetail.AmmountPerMaterial.HasValue && unitPrice > 0)
                        {
                            decimal amountPerUnit = materialDetail.AmmountPerMaterial.Value;
                            
                            // Calculation: EstimatedAmount = (QuantityPerHa / AmmountPerMaterial) * PricePerMaterial * effectiveTotalArea
                            decimal pricePerHa = Math.Ceiling(materialDto.QuantityPerHa / amountPerUnit) * unitPrice;
                            estimatedAmount = pricePerHa * effectiveTotalArea;
                        }
                        else
                        {
                            _logger.LogWarning("Cannot calculate cost for Material ID {MId}: Material details or price is missing/zero.", materialDto.MaterialId);
                        }
                        
                        material.EstimatedAmount = estimatedAmount;
                        totalTaskMaterialCost += estimatedAmount;
                        
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
            // Lưu ý: Chỉ cần gọi SaveChangesAsync() một lần trên UoW hoặc Repository gốc nếu không cần các lệnh riêng biệt
            await _unitOfWork.Repository<ProductionPlan>().SaveChangesAsync();

            _logger.LogInformation("Successfully created new ProductionPlan with ID {PlanId}, {StageCount} stages, and {TaskCount} tasks. Area used: {Area}", plan.Id, planStages.Count, allPlanTasks.Count, effectiveTotalArea);

            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' created successfully with total area {effectiveTotalArea} ha.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating production plan: {PlanName}", request.PlanName);

            // Lỗi DbUpdateException là do lỗi DateTime.Kind
            if (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx && dbEx.InnerException is System.ArgumentException argEx && argEx.Message.Contains("Cannot write DateTime with Kind=Unspecified"))
            {
                 return Result<Guid>.Failure("Database save failed due to Date/Time format error (check UTC kind).", "DateTimeKindError");
            }

            return Result<Guid>.Failure("An error occurred while creating the production plan.", "CreateProductionPlan failed.");
        }
    }
}
