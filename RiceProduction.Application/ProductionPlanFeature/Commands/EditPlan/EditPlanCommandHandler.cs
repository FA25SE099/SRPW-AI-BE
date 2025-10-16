using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.ProductionPlanFeature.Commands.EditPlan;

public class EditPlanCommandHandler :
    IRequestHandler<EditPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EditPlanCommandHandler> _logger;
    private readonly IUser _currentUser; // <-- Đã thêm ICurrentUserService

    public EditPlanCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<EditPlanCommandHandler> logger,
        IUser currentUser) // <-- Inject IUser
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentUser = currentUser; // <-- Khởi tạo
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
            plan.BasePlantingDate = request.BasePlantingDate;
            plan.LastModified = DateTime.UtcNow;
            plan.LastModifiedBy = expertId; // <-- Gán ID chuyên gia

            // --- 4. Prepare Material Prices for Recalculation (Logic remains the same) ---
            var materialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = request.BasePlantingDate.Date;
            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate
            );

            var materialPrices = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p.PricePerMaterial);

            // --- 5. Clean up old stages/tasks/materials ---
            _unitOfWork.Repository<ProductionStage>().DeleteRange(plan.CurrentProductionStages);
            plan.CurrentProductionStages.Clear();

            // --- 6. Create NEW Stage/Task/Material Graph (Logic remains the same) ---
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
                };

                // ... (Task and Material creation/recalculation logic remains the same)

                foreach (var taskDto in stageDto.Tasks.OrderBy(t => t.SequenceOrder))
                {
                    var newTask = new ProductionPlanTask
                    {
                        ProductionStage = newStage,
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

                    foreach (var materialDto in taskDto.Materials)
                    {
                        var newMaterial = new ProductionPlanTaskMaterial
                        {
                            MaterialId = materialDto.MaterialId,
                            QuantityPerHa = materialDto.QuantityPerHa,
                            ProductionPlanTask = newTask
                        };

                        // Recalculation logic:
                        var unitPrice = materialPrices.GetValueOrDefault(materialDto.MaterialId, 0M);
                        decimal totalQuantity = materialDto.QuantityPerHa * effectiveTotalArea;
                        newMaterial.EstimatedAmount = totalQuantity * unitPrice;

                        totalTaskMaterialCost += newMaterial.EstimatedAmount.GetValueOrDefault(0);
                        newMaterial.LastModifiedBy = expertId; // Set last modified ID
                        newTask.ProductionPlanTaskMaterials.Add(newMaterial);
                    }
                    newTask.EstimatedMaterialCost = totalTaskMaterialCost;
                    newTask.LastModifiedBy = expertId; // Set last modified ID
                    newStage.ProductionPlanTasks.Add(newTask);
                }
                newStage.LastModifiedBy = expertId; // Set last modified ID
                newStages.Add(newStage);
            }

            plan.CurrentProductionStages = newStages;

            // --- 7. Save Changes ---
            _unitOfWork.Repository<ProductionPlan>().Update(plan);
            await _unitOfWork.Repository<ProductionPlan>().SaveChangesAsync();

            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing production plan: {PlanId}", request.PlanId);

            return Result<Guid>.Failure("An error occurred while editing the production plan.", "EditProductionPlanFailed");
        }
    }
}