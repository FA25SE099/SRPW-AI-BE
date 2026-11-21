using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Common;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Text.Json;

namespace RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;

public class CreateEmergencyReportCommandHandler : IRequestHandler<CreateEmergencyReportCommand, Result<Guid>>
{
    private static readonly HashSet<string> ValidAlertTypes = new() { "pest", "weather", "disease", "other" };

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
            var fieldValidation = ValidateRequiredFields(request, out var normalizedAlertType);
            if (fieldValidation != null)
            {
                return fieldValidation;
            }

            var entityValidation = await ValidateReferencedEntitiesAsync(request);
            if (entityValidation != null)
            {
                return entityValidation;
            }

            var source = ResolveAlertSource(_currentUser.Roles);

            var uploadedUrls = new List<string>();
            if (request.Images != null && request.Images.Any())
            {
                var folder = $"emergency-reports/{normalizedAlertType.ToLower()}";
                var uploadTasks = request.Images.Select(file => _storageService.UploadAsync(file, folder));
                var results = await Task.WhenAll(uploadTasks);
                uploadedUrls = results.Select(r => r.Url).ToList();
            }

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

            await _unitOfWork.Repository<EmergencyReport>().AddAsync(emergencyReport);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Emergency report created successfully. ID: {ReportId}, Type: {AlertType}, Source: {Source}, Reporter: {UserId}, Images: {ImageCount}, AI Analysis: {HasAI}, AffectedTask: {TaskId}",
                emergencyReport.Id, emergencyReport.AlertType, emergencyReport.Source, userId.Value, uploadedUrls.Count, emergencyReport.HasAiAnalysis, emergencyReport.AffectedCultivationTaskId);

            var message = uploadedUrls.Any()
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

    private static Result<Guid>? ValidateRequiredFields(
        CreateEmergencyReportCommand request,
        out string normalizedAlertType)
    {
        normalizedAlertType = string.Empty;

        if (!request.PlotCultivationId.HasValue && !request.GroupId.HasValue && !request.ClusterId.HasValue)
        {
            return Result<Guid>.Failure("At least one affected entity (PlotCultivation, Group, or Cluster) must be specified.");
        }

        var alertType = request.AlertType?.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(alertType))
        {
            return Result<Guid>.Failure("Alert type is required. Must be 'Pest', 'Weather', 'Disease', or 'Other'.");
        }

        if (!ValidAlertTypes.Contains(alertType))
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

        normalizedAlertType = char.ToUpper(alertType[0]) + alertType.Substring(1);
        return null;
    }

    private async Task<Result<Guid>?> ValidateReferencedEntitiesAsync(CreateEmergencyReportCommand request)
    {
        var plotValidation = await ValidateOptionalEntityExistsAsync<PlotCultivation>(request.PlotCultivationId, "PlotCultivation");
        if (plotValidation != null)
        {
            return plotValidation;
        }

        var groupValidation = await ValidateOptionalEntityExistsAsync<Group>(request.GroupId, "Group");
        if (groupValidation != null)
        {
            return groupValidation;
        }

        if (request.ClusterId.HasValue)
        {
            var clusterExists = await _unitOfWork.ClusterRepository!.ExistClusterAsync(request.ClusterId.Value);
            if (!clusterExists)
            {
                return Result<Guid>.Failure($"Cluster with ID {request.ClusterId.Value} not found.");
            }
        }

        return await ValidateOptionalEntityExistsAsync<CultivationTask>(request.AffectedCultivationTaskId, "Cultivation Task");
    }

    private async Task<Result<Guid>?> ValidateOptionalEntityExistsAsync<TEntity>(
        Guid? entityId,
        string entityDisplayName)
        where TEntity : BaseAuditableEntity
    {
        if (!entityId.HasValue)
        {
            return null;
        }

        var exists = await _unitOfWork.Repository<TEntity>().ExistsAsync(e => e.Id == entityId.Value);
        return exists
            ? null
            : Result<Guid>.Failure($"{entityDisplayName} with ID {entityId.Value} not found.");
    }

    private static AlertSource ResolveAlertSource(IEnumerable<string>? userRoles)
    {
        var roles = userRoles?.ToList();
        if (roles == null)
        {
            return AlertSource.System;
        }

        if (roles.Contains("Farmer"))
        {
            return AlertSource.FarmerReport;
        }

        if (roles.Contains("Supervisor"))
        {
            return AlertSource.SupervisorInspection;
        }

        return AlertSource.System;
    }
}