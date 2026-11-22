using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.UAVFeature.Commands.ReportServiceOrder;
public class ReportServiceOrderCompletionCommandHandler : IRequestHandler<ReportServiceOrderCompletionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<ReportServiceOrderCompletionCommandHandler> _logger;
    
    public ReportServiceOrderCompletionCommandHandler(
        IApplicationDbContext context,
        IStorageService storageService,
        ILogger<ReportServiceOrderCompletionCommandHandler> logger)
    {
        _context = context;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(ReportServiceOrderCompletionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Tải Order và Plot Assignment
            var order = await _context.UavServiceOrders
                .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.UavVendorId == request.VendorId, cancellationToken);
            if (order == null) return Result<Guid>.Failure("UAV Service Order not found or unauthorized.", "OrderNotFound");

            // 1b. Tìm Assignment cụ thể cho Plot
            var assignment = await _context.UavOrderPlotAssignments
                .Include(a => a.CultivationTask)
                .FirstOrDefaultAsync(a => a.UavServiceOrderId == request.OrderId && a.PlotId == request.PlotId, cancellationToken);

            if (assignment == null)
            {
                 // Nếu không tìm thấy assignment, có thể là lỗi data hoặc Order mới cần tạo Assignment
                 return Result<Guid>.Failure($"Plot {request.PlotId} is not assigned to Order {request.OrderId}.", "PlotNotAssigned");
            }
            
            // Nếu đã hoàn thành, không cho báo cáo lại
            if (assignment.Status == RiceProduction.Domain.Enums.TaskStatus.Completed)
            {
                 return Result<Guid>.Failure("This plot assignment is already completed.", "AssignmentCompleted");
            }

            // 2. Upload Files
            var uploadedUrls = new List<string>();
            if (request.ProofFiles.Any())
            {
                string folder = $"uav-plot-reports/{request.OrderId}/{request.PlotId}";
                var results = await Task.WhenAll(request.ProofFiles.Select(file => _storageService.UploadAsync(file, folder)));
                uploadedUrls = results.Select(r => r.Url).ToList();
            }

            // 3. Cập nhật Plot Assignment
            assignment.Status = RiceProduction.Domain.Enums.TaskStatus.Completed; // Hoàn thành Plot này
            assignment.CompletionDate = DateTime.UtcNow;
            assignment.ActualCost = request.ActualCost;
            assignment.ReportNotes = request.Notes;
            assignment.ServicedArea = request.ActualAreaCovered;
            assignment.ProofUrlsJson = JsonSerializer.Serialize(uploadedUrls); // Lưu URLs

            var cultivationTask = assignment.CultivationTask;

            if (cultivationTask != null)
            {
                cultivationTask.ActualEndDate = DateTime.UtcNow;
                _logger.LogInformation("CultivationTask {TaskId} status updated to Completed via UAV report.", cultivationTask.Id);
            }

            // 4. Tổng hợp và Cập nhật Order (Aggregate Logic)
            await _context.SaveChangesAsync(cancellationToken); // Lưu Assignment trước

            // Tải tất cả Assignments để tính toán tổng
            var allAssignments = await _context.UavOrderPlotAssignments
                .AsNoTracking()
                .Where(a => a.UavServiceOrderId == request.OrderId)
                .Select(a => new { a.Status, a.ActualCost })
                .ToListAsync(cancellationToken);

            var totalCompletedPlots = allAssignments.Count(a => a.Status == RiceProduction.Domain.Enums.TaskStatus.Completed);
            var totalActualCost = allAssignments.Sum(a => a.ActualCost.GetValueOrDefault(0M));
            var completionPercentage = order.TotalPlots > 0
                ? (int)Math.Round((double)totalCompletedPlots / order.TotalPlots * 100)
                : 0;

            order.ActualCost = totalActualCost;
            order.CompletionPercentage = completionPercentage;
            
            // Cập nhật trạng thái Order nếu hoàn thành 100%
            if (completionPercentage == 100)
            {
                order.Status = RiceProduction.Domain.Enums.TaskStatus.Completed;
                order.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("UAV Order {OrderId} fully completed.", order.Id);
            } 
            else if (order.Status == RiceProduction.Domain.Enums.TaskStatus.Approved)
            {
                // Nếu đây là Plot đầu tiên, đánh dấu Order là InProgress
                order.Status = RiceProduction.Domain.Enums.TaskStatus.InProgress;
                order.StartedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Plot {PlotId} reported completed for Order {OrderId}. Overall Completion: {Percent}%", request.PlotId, order.Id, completionPercentage);
            
            return Result<Guid>.Success(assignment.Id, $"Report for Plot {request.PlotId} submitted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting UAV service completion for Order {OrderId}, Plot {PlotId}", request.OrderId, request.PlotId);
            return Result<Guid>.Failure("Failed to submit service report.", "ReportCompletionFailed");
        }
    }
    private async Task UpdateNextTaskToInProgress(Guid plotCultivationId, Guid? versionId, int? currentExecutionOrder, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!currentExecutionOrder.HasValue)
            {
                _logger.LogWarning("Current task has no ExecutionOrder, cannot determine next task.");
                return;
            }

            // Find the next task with higher ExecutionOrder for the same PlotCultivation and Version
            var nextTask = await _context.CultivationTasks
                .Where(t => t.PlotCultivationId == plotCultivationId
                          && t.VersionId == versionId
                          && t.ExecutionOrder.HasValue
                          && t.ExecutionOrder > currentExecutionOrder
                          && (t.Status == RiceProduction.Domain.Enums.TaskStatus.Approved
                              || t.Status == RiceProduction.Domain.Enums.TaskStatus.Draft))
                .OrderBy(t => t.ExecutionOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextTask != null)
            {
                nextTask.Status = RiceProduction.Domain.Enums.TaskStatus.InProgress;
                nextTask.ActualStartDate = DateTime.UtcNow;
                
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
            // Don't throw - this is a secondary operation
        }
    }
}