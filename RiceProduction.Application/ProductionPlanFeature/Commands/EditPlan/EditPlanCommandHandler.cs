using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ProductionPlanFeature.Commands.EditPlan;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

// Giả định rằng ProductionStageRequest, ProductionPlanTaskRequest, ProductionPlanTaskMaterialRequest
// đã được định nghĩa trong CreateProductionPlanCommand.cs và được tham chiếu ở đây.

namespace RiceProduction.Application.ProductionPlanFeature.Commands.EditPlan;

public class EditPlanCommandHandler : 
    IRequestHandler<EditPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EditPlanCommandHandler> _logger;
    private readonly IUser _currentUser; 

    public EditPlanCommandHandler(
        IUnitOfWork unitOfWork, 
        ILogger<EditPlanCommandHandler> logger,
        IUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(EditPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Lấy ID của người dùng hiện tại
            var expertId = _currentUser.Id;

            if (expertId == null)
            {
                return Result<Guid>.Failure("Current expert user ID not found.", "AuthenticationRequired");
            }
            
            // --- 1. Load Existing Plan and validate status ---
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(
                match: p => p.Id == request.PlanId,
                includeProperties: q => q
                    .Include(p => p.Group)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks)
                            .ThenInclude(t => t.ProductionPlanTaskMaterials)
            );

            if (plan == null)
            {
                return Result<Guid>.Failure($"Production Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            if (plan.Status != RiceProduction.Domain.Enums.TaskStatus.Draft && plan.Status != RiceProduction.Domain.Enums.TaskStatus.PendingApproval)
            {
                return Result<Guid>.Failure("Plan can only be edited when in Draft or Submitted status.", "InvalidStatusForEdit");
            }
            
            // --- 2. Determine Area for Recalculation ---
            decimal effectiveTotalArea = plan.TotalArea ?? 0M;
            if (effectiveTotalArea <= 0M && plan.GroupId.HasValue && plan.Group != null)
            {
                effectiveTotalArea = plan.Group.TotalArea.GetValueOrDefault(0M);
            }

            if (effectiveTotalArea <= 0M)
            {
                 return Result<Guid>.Failure("Plan area is undefined. Cannot recalculate costs.", "AreaMissing");
            }

            // --- 3. Update main Plan properties ---
            plan.PlanName = request.PlanName;
            
            // Chuyển đổi BasePlantingDate sang UTC Kind để lưu DB an toàn
            plan.BasePlantingDate = DateTime.SpecifyKind(request.BasePlantingDate, DateTimeKind.Utc);
            
            plan.LastModified = DateTime.UtcNow;
            plan.LastModifiedBy = expertId;
            
            // --- 4. Prepare Material Prices and Details for Recalculation ---
            var materialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.BasePlantingDate.Date, DateTimeKind.Utc);

            // Truy vấn và tạo MaterialPrice Map
            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate
            );
            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p.PricePerMaterial);
            
            // Truy vấn và tạo Material Detail Map
            var materialDetailsList = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => materialIds.Contains(m.Id)
            );
            var materialDetailMap = materialDetailsList.ToDictionary(m => m.Id, m => m);

            
            // --- 5. Clean up old stages/tasks/materials ---
            // Xóa các entity con để tránh lỗi theo dõi và đảm bảo đồng bộ với request mới
            var oldStages = plan.CurrentProductionStages.ToList();
            if (oldStages.Any())
            {
                // Xóa Materials trước, sau đó là Tasks, và cuối cùng là Stages
                var oldTasks = oldStages.SelectMany(s => s.ProductionPlanTasks).ToList();
                var oldMaterials = oldTasks.SelectMany(t => t.ProductionPlanTaskMaterials).ToList();
                
                _unitOfWork.Repository<ProductionPlanTaskMaterial>().DeleteRange(oldMaterials);
                _unitOfWork.Repository<ProductionPlanTask>().DeleteRange(oldTasks);
                _unitOfWork.Repository<ProductionStage>().DeleteRange(oldStages);

                plan.CurrentProductionStages.Clear();
            }


            // --- 6. Create NEW Stage/Task/Material Graph ---
            var newStages = new List<ProductionStage>();

            foreach (var stageDto in request.Stages.OrderBy(s => s.SequenceOrder))
            {
                var newStage = new ProductionStage
                {
                    ProductionPlan = plan,
                    StageName = stageDto.StageName,
                    Description = stageDto.Description,
                    SequenceOrder = stageDto.SequenceOrder,
                    TypicalDurationDays = stageDto.TypicalDurationDays,
                    ColorCode = stageDto.ColorCode,
                    IsActive = true,
                    LastModifiedBy = expertId
                };
                
                foreach (var taskDto in stageDto.Tasks.OrderBy(t => t.SequenceOrder))
                {
                    // Chuyển đổi ScheduledDate sang UTC Kind
                    var scheduledDateUtc = DateTime.SpecifyKind(taskDto.ScheduledDate, DateTimeKind.Utc);
                    var scheduledEndDateUtc = taskDto.ScheduledEndDate.HasValue 
                        ? DateTime.SpecifyKind(taskDto.ScheduledEndDate.Value, DateTimeKind.Utc) 
                        : (DateTime?)null;

                    var newTask = new ProductionPlanTask
                    {
                        ProductionStage = newStage,
                        TaskName = taskDto.TaskName,
                        Description = taskDto.Description,
                        TaskType = taskDto.TaskType,
                        ScheduledDate = scheduledDateUtc,
                        ScheduledEndDate = scheduledEndDateUtc,
                        Priority = taskDto.Priority,
                        SequenceOrder = taskDto.SequenceOrder,
                        Status = RiceProduction.Domain.Enums.TaskStatus.Draft, // Reset task status on major edit
                        LastModifiedBy = expertId
                    };
                    
                    decimal totalTaskMaterialCost = 0;

                    foreach (var materialDto in taskDto.Materials)
                    {
                        var newMaterial = new ProductionPlanTaskMaterial
                        {
                            MaterialId = materialDto.MaterialId,
                            QuantityPerHa = materialDto.QuantityPerHa,
                            ProductionPlanTask = newTask,
                            LastModifiedBy = expertId
                        };
                        
                        // Recalculation logic:
                        decimal unitPrice = materialPriceMap.GetValueOrDefault(materialDto.MaterialId, 0M);
                        materialDetailMap.TryGetValue(materialDto.MaterialId, out var materialDetail);
                        
                        decimal estimatedAmount = 0M;

                        if (materialDetail != null && materialDetail.AmmountPerMaterial.HasValue && unitPrice > 0)
                        {
                            decimal amountPerUnit = materialDetail.AmmountPerMaterial.Value;
                            
                            // Calculation: EstimatedAmount = (QuantityPerHa / AmmountPerMaterial) * PricePerMaterial * effectiveTotalArea
                            decimal pricePerHa = (materialDto.QuantityPerHa / amountPerUnit) * unitPrice;
                            estimatedAmount = pricePerHa * effectiveTotalArea;
                        }
                        
                        newMaterial.EstimatedAmount = estimatedAmount;
                        totalTaskMaterialCost += newMaterial.EstimatedAmount.GetValueOrDefault(0);
                        newTask.ProductionPlanTaskMaterials.Add(newMaterial);
                    }
                    
                    newTask.EstimatedMaterialCost = totalTaskMaterialCost;
                    newStage.ProductionPlanTasks.Add(newTask);
                }
                newStages.Add(newStage);
            }
            
            // Gán lại tập hợp Stages mới
            plan.CurrentProductionStages = newStages;
            
            // --- 7. Save Changes ---
            _unitOfWork.Repository<ProductionPlan>().Update(plan);
            
            // EF Core sẽ theo dõi và thực hiện các lệnh DeleteRange, Update, Insert/Add khi SaveChangesAsync được gọi
            await _unitOfWork.Repository<ProductionPlan>().SaveChangesAsync(); 

            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing production plan: {PlanId}", request.PlanId);

            // Bắt lỗi Foreign Key Violation để thông báo rõ ràng hơn
            if (ex is DbUpdateException dbEx && dbEx.InnerException is Npgsql.PostgresException pgEx)
            {
                 if (pgEx.SqlState == "23503" && pgEx.TableName == "ProductionPlanTaskMaterials")
                {
                    return Result<Guid>.Failure("Validation failed: One or more Material IDs do not exist.", "MaterialIdInvalid");
                }
            }

            return Result<Guid>.Failure("An error occurred while editing the production plan.", "EditProductionPlanFailed");
        }
    }
}