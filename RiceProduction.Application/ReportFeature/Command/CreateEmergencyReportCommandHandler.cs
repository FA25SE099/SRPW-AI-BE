using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Text.Json;

namespace RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;

public class CreateEmergencyReportCommandHandler : IRequestHandler<CreateEmergencyReportCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IStorageService _storageService;
    private readonly ILogger<CreateEmergencyReportCommandHandler> _logger;

    public CreateEmergencyReportCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IStorageService storageService,
        ILogger<CreateEmergencyReportCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateEmergencyReportCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;
        if (!userId.HasValue)
        {
            return Result<Guid>.Failure("User not authenticated.", "Unauthorized");
        }

        try
        {
            if (!request.PlotCultivationId.HasValue && !request.GroupId.HasValue && !request.ClusterId.HasValue)
            {
                return Result<Guid>.Failure("At least one affected entity (PlotCultivation, Group, or Cluster) must be specified.");
            }

            // 3. Validate required fields
            var alertType = request.AlertType?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(alertType))
            {
                return Result<Guid>.Failure("Alert type is required. Must be 'Pest', 'Weather', 'Disease', or 'Other'.");
            }

            if (alertType != "pest" && alertType != "weather" && alertType != "disease" && alertType != "other")
            {
                return Result<Guid>.Failure("Alert type must be either 'Pest', 'Weather', 'Disease', or 'Other'.");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Result<Guid>.Failure("Title is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return Result<Guid>.Failure("Description is required.");
            }

            if (request.PlotCultivationId.HasValue)
            {
                var plotExists = await _unitOfWork.Repository<PlotCultivation>()
                    .ExistsAsync(c => c.Id == request.PlotCultivationId);

                if (!plotExists)
                {
                    return Result<Guid>.Failure($"PlotCultivation with ID {request.PlotCultivationId.Value} not found.");
                }
            }

            if (request.GroupId.HasValue)
            {
                var groupExists = await _unitOfWork.Repository<Group>()
                    .ExistsAsync(c => c.Id == request.GroupId);

                if (!groupExists)
                {
                    return Result<Guid>.Failure($"Group with ID {request.GroupId.Value} not found.");
                }
            }

            if (request.ClusterId.HasValue)
            {
                var clusterExists = await _unitOfWork.ClusterRepository!
                    .ExistClusterAsync(request.ClusterId.Value);

                if (!clusterExists)
                {
                    return Result<Guid>.Failure($"Cluster with ID {request.ClusterId.Value} not found.");
                }
            }

            // Validate AffectedCultivationTaskId if provided
            if (request.AffectedCultivationTaskId.HasValue)
            {
                var taskExists = await _unitOfWork.Repository<CultivationTask>()
                    .ExistsAsync(t => t.Id == request.AffectedCultivationTaskId.Value);

                if (!taskExists)
                {
                    return Result<Guid>.Failure($"Cultivation Task with ID {request.AffectedCultivationTaskId.Value} not found.");
                }
            }

            // 5. Determine the source based on user role
            var userRoles = _currentUser.Roles ?? new List<string>();
            AlertSource source;

            if (userRoles.Contains("Farmer"))
            {
                source = AlertSource.FarmerReport;
            }
            else if (userRoles.Contains("Supervisor"))
            {
                source = AlertSource.SupervisorInspection;
            }
            else
            {
                source = AlertSource.System;
            }

            var normalizedAlertType = char.ToUpper(alertType[0]) + alertType.Substring(1);

            // Upload Images (Parallel Upload)
            var uploadedUrls = new List<string>();
            if (request.Images != null && request.Images.Any())
            {
                string folder = $"emergency-reports/{normalizedAlertType.ToLower()}";
                var uploadTasks = request.Images.Select(file => _storageService.UploadAsync(file, folder));
                var results = await Task.WhenAll(uploadTasks);
                uploadedUrls = results.Select(r => r.Url).ToList();
            }

            var hasImages = uploadedUrls.Any();
            var emergencyReport = new EmergencyReport
            {
                Source = source,
                Severity = request.Severity,
                Status = AlertStatus.Pending,
                PlotCultivationId = request.PlotCultivationId,
                GroupId = request.GroupId,
                ClusterId = request.ClusterId,
                AffectedCultivationTaskId = request.AffectedCultivationTaskId,
                AlertType = normalizedAlertType,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                ImageUrls = uploadedUrls,
                ReportedBy = userId.Value,
                NotificationSentAt = null
            };

            // Store AI Detection Results if provided
            if (request.AiDetectionResult != null && request.AiDetectionResult.HasPest)
            {
                emergencyReport.HasAiAnalysis = true;
                emergencyReport.AiDetectedPestCount = request.AiDetectionResult.TotalDetections;
                emergencyReport.AiDetectedPestNames = request.AiDetectionResult.DetectedPests
                    .Select(p => p.PestName)
                    .Distinct()
                    .ToList();
                emergencyReport.AiAverageConfidence = request.AiDetectionResult.AverageConfidence;
                emergencyReport.AiPestAnalysisRaw = JsonSerializer.Serialize(request.AiDetectionResult);

                _logger.LogInformation(
                    "AI pest detection data included. Detected {PestCount} pest(s) with average confidence {Confidence:F2}%",
                    emergencyReport.AiDetectedPestCount, emergencyReport.AiAverageConfidence);
            }

            // 9. Save to database
            await _unitOfWork.Repository<EmergencyReport>().AddAsync(emergencyReport);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Emergency report created successfully. ID: {ReportId}, Type: {AlertType}, Source: {Source}, Reporter: {UserId}, Images: {ImageCount}, AI Analysis: {HasAI}, AffectedTask: {TaskId}",
                emergencyReport.Id, emergencyReport.AlertType, emergencyReport.Source, userId.Value, uploadedUrls.Count, emergencyReport.HasAiAnalysis, emergencyReport.AffectedCultivationTaskId);

            var message = hasImages
                ? $"Emergency report created successfully with {uploadedUrls.Count} image(s) uploaded."
                : "Emergency report created successfully.";

            if (emergencyReport.HasAiAnalysis)
            {
                message += $" AI detected {emergencyReport.AiDetectedPestCount} pest(s).";
            }

            return Result<Guid>.Success(emergencyReport.Id, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating emergency report for user {UserId}", userId.Value);
            return Result<Guid>.Failure("An error occurred while creating the emergency report.");
        }
    }
}