using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorProfile;

public class GetSupervisorProfileQueryHandler : IRequestHandler<GetSupervisorProfileQuery, Result<SupervisorProfileResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetSupervisorProfileQueryHandler> _logger;

    public GetSupervisorProfileQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetSupervisorProfileQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SupervisorProfileResponse>> Handle(
        GetSupervisorProfileQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Get supervisor with cluster info
            var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(request.SupervisorId);

            if (supervisor == null)
            {
                _logger.LogWarning("Supervisor not found: {SupervisorId}", request.SupervisorId);
                return Result<SupervisorProfileResponse>.Failure(
                    "Supervisor not found.",
                    "NotFound"
                );
            }

            // Load cluster if needed
            Cluster? cluster = null;
            if (supervisor.ClusterId.HasValue)
            {
                cluster = await _unitOfWork.Repository<Cluster>()
                    .FindAsync(c => c.Id == supervisor.ClusterId.Value);
            }

            // Step 2: Get total groups supervised (all time)
            var allGroups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.SupervisorId == request.SupervisorId);
            var totalGroupsSupervised = allGroups.Count;

            // Step 3: Get active groups this season
            var currentDateString = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var activeGroupsThisSeason = 0;
            
            foreach (var group in allGroups)
            {
                if (group.Season != null &&
                    string.Compare(group.Season.StartDate, currentDateString) <= 0 &&
                    string.Compare(group.Season.EndDate, currentDateString) >= 0 &&
                    group.Status == GroupStatus.Active)
                {
                    activeGroupsThisSeason++;
                }
            }

            // Step 4: Get polygon task statistics
            var allPolygonTasks = await _unitOfWork.Repository<PlotPolygonTask>()
                .ListAsync(t => t.AssignedToSupervisorId == request.SupervisorId);

            var completedPolygonTasks = allPolygonTasks.Count(t =>
                t.Status == "Completed"
            );

            var pendingPolygonTasks = allPolygonTasks.Count(t =>
                t.Status == "Pending" || t.Status == "InProgress"
            );

            // Step 5: Map to response
            var response = new SupervisorProfileResponse
            {
                SupervisorId = supervisor.Id,
                FullName = supervisor.FullName ?? string.Empty,
                Email = supervisor.Email ?? string.Empty,
                PhoneNumber = supervisor.PhoneNumber ?? string.Empty,
                Address = supervisor.Address,
                DateOfBirth = null, // ApplicationUser doesn't have DateOfBirth

                ClusterId = supervisor.ClusterId,
                ClusterName = cluster?.ClusterName,

                TotalGroupsSupervised = totalGroupsSupervised,
                ActiveGroupsThisSeason = activeGroupsThisSeason,
                CompletedPolygonTasks = completedPolygonTasks,
                PendingPolygonTasks = pendingPolygonTasks,

                CreatedAt = DateTime.UtcNow, // Using current date as fallback
                IsActive = true // Default to true
            };

            _logger.LogInformation(
                "Successfully retrieved profile for supervisor {SupervisorId}",
                request.SupervisorId
            );

            return Result<SupervisorProfileResponse>.Success(
                response,
                "Successfully retrieved supervisor profile."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving profile for supervisor {SupervisorId}",
                request.SupervisorId
            );

            return Result<SupervisorProfileResponse>.Failure(
                "An error occurred while retrieving supervisor profile.",
                "InternalError"
            );
        }
    }
}
