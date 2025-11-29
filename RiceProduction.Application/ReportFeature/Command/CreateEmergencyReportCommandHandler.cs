using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;

public class CreateEmergencyReportCommandHandler : IRequestHandler<CreateEmergencyReportCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly ILogger<CreateEmergencyReportCommandHandler> _logger;

    public CreateEmergencyReportCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        ILogger<CreateEmergencyReportCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
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
                var clusterExists = await _unitOfWork.ClusterRepository
                    .ExistClusterAsync(request.ClusterId.Value);

                if (!clusterExists)
                {
                    return Result<Guid>.Failure($"Cluster with ID {request.ClusterId.Value} not found.");
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

            var hasImages = request.ImageUrls != null && request.ImageUrls.Any();
            var emergencyReport = new EmergencyReport
            {
                Source = source,
                Severity = request.Severity,
                Status = AlertStatus.Pending,
                PlotCultivationId = request.PlotCultivationId,
                GroupId = request.GroupId,
                ClusterId = request.ClusterId,
                AlertType = normalizedAlertType,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                ImageUrls = request.ImageUrls,
                ReportedBy = userId.Value,
                NotificationSentAt = null
            };

            // 9. Save to database
            await _unitOfWork.Repository<EmergencyReport>().AddAsync(emergencyReport);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Emergency report created successfully. ID: {ReportId}, Type: {AlertType}, Source: {Source}, Reporter: {UserId}, Images: {ImageCount}",
                emergencyReport.Id, emergencyReport.AlertType, emergencyReport.Source, userId.Value, emergencyReport.ImageUrls?.Count ?? 0);

            var message = hasImages
                ? "Emergency report created successfully. ."
                : "Emergency report created successfully.";

            return Result<Guid>.Success(emergencyReport.Id, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating emergency report for user {UserId}", userId.Value);
            return Result<Guid>.Failure("An error occurred while creating the emergency report.");
        }
    }
}