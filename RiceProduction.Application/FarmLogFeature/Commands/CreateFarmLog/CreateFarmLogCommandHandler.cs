using System.Text.Json;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;
public class CreateFarmLogCommandHandler : IRequestHandler<CreateFarmLogCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService _storageService; // Inject Storage Service
    private readonly ILogger<CreateFarmLogCommandHandler> _logger;

    public CreateFarmLogCommandHandler(
        IUnitOfWork unitOfWork,
        IStorageService storageService,
        ILogger<CreateFarmLogCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateFarmLogCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Validate Task & Permission
            var task = await _unitOfWork.Repository<CultivationTask>()
                .FindAsync(t => t.Id == request.CultivationTaskId && t.PlotCultivationId == request.PlotCultivationId);

            if (task == null)
            {
                return Result<Guid>.Failure("Cultivation Task not found or does not match Plot.", "TaskNotFound");
            }
            
            // --- NEW VERSIONING CHECK ---
            // 1b. Lấy thông tin PlotCultivation và Version
            var plotCultivation = await _unitOfWork.Repository<PlotCultivation>().FindAsync(
                pc => pc.Id == task.PlotCultivationId,
                includeProperties: q => q.Include(pc => pc.CultivationVersions.Where(v => v.IsActive))
            );
            
            var activeVersion = plotCultivation?.CultivationVersions.FirstOrDefault();
            
            if (activeVersion == null || task.VersionId != activeVersion.Id)
            {
                // Nếu Task không thuộc phiên bản đang hoạt động/mới nhất, từ chối ghi Log
                return Result<Guid>.Failure("Task does not belong to the active plan version. Please refresh.", "VersionConflict");
            }

            if (task.Status == RiceProduction.Domain.Enums.TaskStatus.Completed || task.Status == RiceProduction.Domain.Enums.TaskStatus.Cancelled)
            {
               // return Result<Guid>.Failure("Task is already completed or cancelled.", "TaskClosed");
            }

            // 2. Upload Images (Parallel Upload)
            var uploadedUrls = new List<string>();
            if (request.ProofImages.Any())
            {
                string folder = $"farm-logs/{task.Id}";
                var uploadTasks = request.ProofImages.Select(file => _storageService.UploadAsync(file, folder));
                var results = await Task.WhenAll(uploadTasks);
                uploadedUrls = results.Select(r => r.Url).ToList();
            }

            // 3. Create FarmLog Entity
            var farmLog = new FarmLog
            {
                CultivationTaskId = request.CultivationTaskId,
                PlotCultivationId = request.PlotCultivationId,
                LoggedBy = request.FarmerId!.Value,
                LoggedDate = DateTime.UtcNow,
                WorkDescription = request.WorkDescription,
                CompletionPercentage = 100, // Giả định log này đánh dấu hoàn thành
                ActualAreaCovered = request.ActualAreaCovered,
                ServiceCost = request.ServiceCost,
                ServiceNotes = request.ServiceNotes,
                PhotoUrls = uploadedUrls.ToArray(),
                WeatherConditions = request.WeatherConditions,
                InterruptionReason = request.InterruptionReason,
            };

            // 4. Process Materials & Calculate Costs
            var logMaterials = new List<FarmLogMaterial>();
            decimal totalLogMaterialCost = 0;

            if (request.Materials.Any())
            {
                var materialIds = request.Materials.Select(m => m.MaterialId).Distinct().ToList();
                
                var today = DateTime.UtcNow.Date;
                var prices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                    filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom <= today
                );
                
                var priceMap = prices
                    .GroupBy(p => p.MaterialId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.ValidFrom).FirstOrDefault()?.PricePerMaterial ?? 0);

                foreach (var matRequest in request.Materials)
                {
                    decimal unitPrice = priceMap.GetValueOrDefault(matRequest.MaterialId, 0);
                    decimal cost = matRequest.ActualQuantityUsed * unitPrice;

                    var logMaterial = new FarmLogMaterial
                    {
                        FarmLog = farmLog,
                        MaterialId = matRequest.MaterialId,
                        ActualQuantityUsed = matRequest.ActualQuantityUsed,
                        ActualCost = cost,
                        Notes = matRequest.Notes
                    };
                    
                    logMaterials.Add(logMaterial);
                    totalLogMaterialCost += cost;
                }
                
                farmLog.ActualMaterialJson = JsonSerializer.Serialize(request.Materials);
            }

            farmLog.FarmLogMaterials = logMaterials;

            // 5. Update Cultivation Task Status & Totals
            task.Status = RiceProduction.Domain.Enums.TaskStatus.Completed; 
            task.ActualEndDate = DateTime.UtcNow; 
            
            task.ActualMaterialCost += totalLogMaterialCost;
            task.ActualServiceCost += request.ServiceCost.GetValueOrDefault(0);

            if (!task.ActualStartDate.HasValue)
            {
                // Set ActualStartDate from CreatedAt (DateTimeOffset) converting to UTC DateTime
                task.ActualStartDate = task.CreatedAt.UtcDateTime;
            }

            // 5b. Find and Update Next Task to InProgress
            //await UpdateNextTaskToInProgress(task.PlotCultivationId, task.VersionId, task.ExecutionOrder);

            // 6. Save Changes
            await _unitOfWork.Repository<FarmLog>().AddAsync(farmLog);
            _unitOfWork.Repository<CultivationTask>().Update(task);

            await _unitOfWork.Repository<FarmLog>().SaveChangesAsync();

            _logger.LogInformation("Farm log created for Task {TaskId} by Farmer {FarmerId}. Images: {ImgCount}", task.Id, request.FarmerId, uploadedUrls.Count);

            return Result<Guid>.Success(farmLog.Id, "Farm log submitted and task marked as completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating farm log for Task {TaskId}", request.CultivationTaskId);
            return Result<Guid>.Failure("Failed to submit farm log.", "CreateFarmLogFailed");
        }
    }

    /// <summary>
    /// Finds the next task in execution order and updates it to InProgress
    /// </summary>
    private async Task UpdateNextTaskToInProgress(Guid plotCultivationId, Guid? versionId, int? currentExecutionOrder)
    {
        try
        {
            if (!currentExecutionOrder.HasValue)
            {
                _logger.LogWarning("Current task has no ExecutionOrder, cannot determine next task.");
                return;
            }

            // Find the next task with higher ExecutionOrder for the same PlotCultivation and Version
            var nextTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: t => t.PlotCultivationId == plotCultivationId 
                          && t.VersionId == versionId
                          && t.ExecutionOrder.HasValue
                          && t.ExecutionOrder > currentExecutionOrder
                          && (t.Status == RiceProduction.Domain.Enums.TaskStatus.Approved 
                              || t.Status == RiceProduction.Domain.Enums.TaskStatus.Draft),
                orderBy: q => q.OrderBy(t => t.ExecutionOrder)
            );

            var nextTask = nextTasks.FirstOrDefault();

            if (nextTask != null)
            {
                nextTask.Status = RiceProduction.Domain.Enums.TaskStatus.InProgress;
                nextTask.ActualStartDate = DateTime.UtcNow;
                
                _unitOfWork.Repository<CultivationTask>().Update(nextTask);
                
                _logger.LogInformation(
                    "Next task {NextTaskId} (Order: {Order}) automatically updated to InProgress after completing current task.",
                    nextTask.Id, nextTask.ExecutionOrder);
            }
            else
            {
                _logger.LogInformation("No eligible next task found for PlotCultivation {PlotId}.", plotCultivationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating next task to InProgress for PlotCultivation {PlotId}", plotCultivationId);
        }
    }
}