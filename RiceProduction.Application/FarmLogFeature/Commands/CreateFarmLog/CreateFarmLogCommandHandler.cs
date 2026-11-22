using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;

public class CreateFarmLogCommandHandler : IRequestHandler<CreateFarmLogCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<CreateFarmLogCommandHandler> _logger;

    public CreateFarmLogCommandHandler(
        IApplicationDbContext context,
        IStorageService storageService,
        ILogger<CreateFarmLogCommandHandler> logger)
    {
        _context = context;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateFarmLogCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _context.CultivationTasks
                .FirstOrDefaultAsync(t => t.Id == request.CultivationTaskId && t.PlotCultivationId == request.PlotCultivationId, cancellationToken);

            if (task == null)
            {
                return Result<Guid>.Failure("Cultivation Task not found or does not match Plot.", "TaskNotFound");
            }

            var activeVersionId = await _context.CultivationVersions
                .AsNoTracking()
                .Where(v => v.PlotCultivationId == task.PlotCultivationId && v.IsActive)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeVersionId == null || task.VersionId != activeVersionId)
            {
                return Result<Guid>.Failure("Task does not belong to the active plan version. Please refresh.", "VersionConflict");
            }

            var uploadedUrls = await UploadProofImagesAsync(request, task.Id);
            var now = DateTime.UtcNow;

            var farmLog = new FarmLog
            {
                Id = Guid.NewGuid(),
                CultivationTaskId = request.CultivationTaskId,
                PlotCultivationId = request.PlotCultivationId,
                LoggedBy = request.FarmerId!.Value,
                LoggedDate = now,
                WorkDescription = request.WorkDescription,
                CompletionPercentage = 100,
                ActualAreaCovered = request.ActualAreaCovered,
                ServiceCost = request.ServiceCost,
                ServiceNotes = request.ServiceNotes,
                PhotoUrls = uploadedUrls.ToArray(),
                WeatherConditions = request.WeatherConditions,
                InterruptionReason = request.InterruptionReason
            };

            var (logMaterials, totalLogMaterialCost) = await BuildFarmLogMaterialsAsync(request, farmLog, now, cancellationToken);
            farmLog.FarmLogMaterials = logMaterials;

            // Emergency tasks require supervisor approval before being marked completed
            if (task.Status == TaskStatus.Emergency)
            {
                task.Status = TaskStatus.EmergencyApproval;
                _logger.LogInformation("Emergency task {TaskId} updated to EmergencyApproval status", task.Id);
            }
            else
            {
                task.Status = TaskStatus.Completed;
            }

            task.ActualEndDate = now;
            task.ActualMaterialCost += totalLogMaterialCost;
            task.ActualServiceCost += request.ServiceCost.GetValueOrDefault(0);
            task.ActualStartDate ??= task.CreatedAt.UtcDateTime;

            _context.FarmLogs.Add(farmLog);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Farm log created for Task {TaskId} by Farmer {FarmerId}. Images: {ImgCount}",
                task.Id, request.FarmerId, uploadedUrls.Count);

            return Result<Guid>.Success(farmLog.Id, "Farm log submitted and task marked as completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating farm log for Task {TaskId}", request.CultivationTaskId);
            return Result<Guid>.Failure("Failed to submit farm log.", "CreateFarmLogFailed");
        }
    }

    private async Task<List<string>> UploadProofImagesAsync(CreateFarmLogCommand request, Guid taskId)
    {
        if (!request.ProofImages.Any())
        {
            return new List<string>();
        }

        var folder = $"farm-logs/{taskId}";
        var uploadTasks = request.ProofImages.Select(file => _storageService.UploadAsync(file, folder));
        var results = await Task.WhenAll(uploadTasks);
        return results.Select(r => r.Url).ToList();
    }

    private async Task<(List<FarmLogMaterial> Materials, decimal TotalCost)> BuildFarmLogMaterialsAsync(
        CreateFarmLogCommand request,
        FarmLog farmLog,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var logMaterials = new List<FarmLogMaterial>();
        decimal totalCost = 0;

        if (!request.Materials.Any())
        {
            return (logMaterials, totalCost);
        }

        var materialIds = request.Materials.Select(m => m.MaterialId).Distinct().ToList();
        var prices = await _context.MaterialPrices
            .AsNoTracking()
            .Where(p => materialIds.Contains(p.MaterialId)
                     && p.ValidFrom <= now
                     && (!p.ValidTo.HasValue || p.ValidTo > now))
            .OrderByDescending(p => p.ValidFrom)
            .Select(p => new { p.MaterialId, p.PricePerMaterial })
            .ToListAsync(cancellationToken);

        var priceMap = prices
            .GroupBy(p => p.MaterialId)
            .ToDictionary(g => g.Key, g => g.First().PricePerMaterial);

        foreach (var matRequest in request.Materials)
        {
            var unitPrice = priceMap.GetValueOrDefault(matRequest.MaterialId, 0);
            var cost = matRequest.ActualQuantityUsed * unitPrice;

            logMaterials.Add(new FarmLogMaterial
            {
                FarmLog = farmLog,
                MaterialId = matRequest.MaterialId,
                ActualQuantityUsed = matRequest.ActualQuantityUsed,
                ActualCost = cost,
                Notes = matRequest.Notes
            });

            totalCost += cost;
        }

        farmLog.ActualMaterialJson = JsonSerializer.Serialize(request.Materials);
        return (logMaterials, totalCost);
    }
}