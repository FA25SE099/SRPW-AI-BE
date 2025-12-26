using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Commands.StartCultivationTask;

public class StartCultivationTaskCommandHandler :
    IRequestHandler<StartCultivationTaskCommand, Result<StartCultivationTaskResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly ILogger<StartCultivationTaskCommandHandler> _logger;

    public StartCultivationTaskCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        ILogger<StartCultivationTaskCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<StartCultivationTaskResponse>> Handle(
        StartCultivationTaskCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the cultivation task with all necessary related data
            var cultivationTask = await _unitOfWork.Repository<CultivationTask>().FindAsync(
                match: ct => ct.Id == request.CultivationTaskId,
                includeProperties: q => q
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.Plot)
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.Season)
                    .Include(ct => ct.PlotCultivation)
                        .ThenInclude(pc => pc.RiceVariety)
                    .Include(ct => ct.ProductionPlanTask));

            if (cultivationTask == null)
            {
                _logger.LogWarning("Cultivation task {TaskId} not found", request.CultivationTaskId);
                return Result<StartCultivationTaskResponse>.Failure("Cultivation task not found");
            }

            // Verify that the current user is the farmer who owns the plot
            var farmerId = _currentUser.Id;
            if (farmerId == null)
            {
                _logger.LogWarning("User is not authenticated");
                return Result<StartCultivationTaskResponse>.Failure("User is not authenticated");
            }

            if (cultivationTask.PlotCultivation.Plot.FarmerId != farmerId)
            {
                _logger.LogWarning(
                    "User {UserId} is not authorized to start task {TaskId} for plot {PlotId}",
                    farmerId,
                    request.CultivationTaskId,
                    cultivationTask.PlotCultivation.PlotId);
                return Result<StartCultivationTaskResponse>.Failure(
                    "You are not authorized to start this task");
            }

            // Validate current task status
            if (cultivationTask.Status == TaskStatus.InProgress)
            {
                _logger.LogInformation(
                    "Task {TaskId} is already in progress",
                    request.CultivationTaskId);
                return Result<StartCultivationTaskResponse>.Failure(
                    "This task is already in progress");
            }

            if (cultivationTask.Status == TaskStatus.Completed)
            {
                _logger.LogWarning(
                    "Attempt to start completed task {TaskId}",
                    request.CultivationTaskId);
                return Result<StartCultivationTaskResponse>.Failure(
                    "This task has already been completed");
            }

            if (cultivationTask.Status == TaskStatus.Cancelled)
            {
                _logger.LogWarning(
                    "Attempt to start cancelled task {TaskId}",
                    request.CultivationTaskId);
                return Result<StartCultivationTaskResponse>.Failure(
                    "This task has been cancelled and cannot be started");
            }

            // Update the task status to InProgress
            var now = DateTime.UtcNow;
            cultivationTask.Status = TaskStatus.InProgress;
            cultivationTask.ActualStartDate = now;
            
            if (!string.IsNullOrWhiteSpace(request.WeatherConditions))
            {
                cultivationTask.WeatherConditions = request.WeatherConditions;
            }

            // If notes are provided, create a farm log entry
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                var farmLog = new FarmLog
                {
                    CultivationTaskId = cultivationTask.Id,
                    PlotCultivationId = cultivationTask.PlotCultivationId,
                    LoggedDate = now,
                    ServiceNotes = request.Notes,
                    WeatherConditions = request.WeatherConditions,
                    LoggedBy = farmerId.Value,
                    CreatedAt = now
                };

                await _unitOfWork.Repository<FarmLog>().AddAsync(farmLog);
            }
            else
            {
                var farmLog = new FarmLog
                {
                    CultivationTaskId = cultivationTask.Id,
                    PlotCultivationId = cultivationTask.PlotCultivationId,
                    LoggedDate = now,
                    ServiceNotes = "Task started: " + cultivationTask.ProductionPlanTask.TaskName,
                    WeatherConditions = request.WeatherConditions,
                    LoggedBy = farmerId.Value,
                    CreatedAt = now
                };

                await _unitOfWork.Repository<FarmLog>().AddAsync(farmLog);
            }

            // Update the PlotCultivation status if it's still in Planned status
            if (cultivationTask.PlotCultivation.Status == Domain.Enums.CultivationStatus.Planned)
            {
                cultivationTask.PlotCultivation.Status = Domain.Enums.CultivationStatus.InProgress;
            }

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Cultivation task {TaskId} started by farmer {FarmerId} at {StartTime}",
                request.CultivationTaskId,
                farmerId,
                now);

            // Prepare response
            var response = new StartCultivationTaskResponse
            {
                CultivationTaskId = cultivationTask.Id,
                TaskName = cultivationTask.CultivationTaskName ?? cultivationTask.ProductionPlanTask?.TaskName ?? "Unknown Task",
                Status = cultivationTask.Status.Value,
                ActualStartDate = cultivationTask.ActualStartDate.Value,
                ScheduledEndDate = cultivationTask.ScheduledEndDate,
                WeatherConditions = cultivationTask.WeatherConditions,
                Message = "Công việc đã bắt đầu thành công",
                PlotId = cultivationTask.PlotCultivation.PlotId,
                PlotReference = $"{cultivationTask.PlotCultivation.Plot.SoThua}/{cultivationTask.PlotCultivation.Plot.SoTo}",
                SeasonName = cultivationTask.PlotCultivation.Season.SeasonName,
                RiceVarietyName = cultivationTask.PlotCultivation.RiceVariety.VarietyName
            };

            return Result<StartCultivationTaskResponse>.Success(
                response,
                "Công việc đã bắt đầu thành công. Chúc bạn may mắn với việc canh tác!");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error starting cultivation task {TaskId}",
                request.CultivationTaskId);
            return Result<StartCultivationTaskResponse>.Failure(
                "Đã xảy ra lỗi khi bắt đầu công việc");
        }
    }
}

