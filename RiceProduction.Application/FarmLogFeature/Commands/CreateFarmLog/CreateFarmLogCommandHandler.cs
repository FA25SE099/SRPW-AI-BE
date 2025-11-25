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

            if (task.Status == RiceProduction.Domain.Enums.TaskStatus.Completed || task.Status == RiceProduction.Domain.Enums.TaskStatus.Cancelled)
            {
               // Tùy logic: có cho phép log bổ sung khi đã xong không? Ở đây giả sử là không.
               return Result<Guid>.Failure("Task is already completed or cancelled.", "TaskClosed");
            }

            // 2. Upload Images (Parallel Upload)
            var uploadedUrls = new List<string>();
            if (request.ProofImages.Any())
            {
                // Định nghĩa folder lưu trữ, ví dụ: farm-logs/{taskId}
                string folder = $"farm-logs/{task.Id}";
                
                // Upload đồng thời để tối ưu tốc độ
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
                // VerifiedBy/At sẽ được Supervisor cập nhật sau
            };

            // 4. Process Materials & Calculate Costs
            var logMaterials = new List<FarmLogMaterial>();
            decimal totalLogMaterialCost = 0;

            if (request.Materials.Any())
            {
                var materialIds = request.Materials.Select(m => m.MaterialId).Distinct().ToList();
                
                // Lấy giá hiện tại của vật tư để tính chi phí thực tế
                var today = DateTime.UtcNow.Date;
                var prices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                    filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom <= today
                );
                
                // Map giá mới nhất cho từng vật tư
                var priceMap = prices
                    .GroupBy(p => p.MaterialId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.ValidFrom).FirstOrDefault()?.PricePerMaterial ?? 0);

                foreach (var matRequest in request.Materials)
                {
                    decimal unitPrice = priceMap.GetValueOrDefault(matRequest.MaterialId, 0);
                    
                    // Giả định ActualQuantityUsed là tổng số đơn vị (kg, lít, chai) đã dùng
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
                
                // Lưu JSON snapshot cho truy vấn nhanh (theo yêu cầu Entity)
                farmLog.ActualMaterialJson = JsonSerializer.Serialize(request.Materials);
            }

            farmLog.FarmLogMaterials = logMaterials;

            // 5. Update Cultivation Task Status & Totals
            // Logic: Cộng dồn chi phí nếu có nhiều logs, hoặc ghi đè nếu log này chốt sổ.
            // Ở đây ta cộng dồn chi phí vào Task.

            task.Status = RiceProduction.Domain.Enums.TaskStatus.Completed; // Đánh dấu hoàn thành
            task.ActualEndDate = DateTime.UtcNow; // Ngày hoàn thành thực tế
            
            // Cập nhật chi phí thực tế tích lũy trên Task
            task.ActualMaterialCost += totalLogMaterialCost;
            task.ActualServiceCost += request.ServiceCost.GetValueOrDefault(0);

            // Nếu là Task đầu tiên (Start), set ActualStartDate nếu chưa có
            if (!task.ActualStartDate.HasValue)
            {
                // Convert DateTimeOffset to DateTime (use UTC to preserve absolute time)
                task.ActualStartDate = task.CreatedAt.UtcDateTime; // Hoặc lấy từ log nếu có trường StartTime
            }

            // 6. Save Changes
            await _unitOfWork.Repository<FarmLog>().AddAsync(farmLog);
            // FarmLogMaterials được lưu tự động nhờ navigation property
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
}