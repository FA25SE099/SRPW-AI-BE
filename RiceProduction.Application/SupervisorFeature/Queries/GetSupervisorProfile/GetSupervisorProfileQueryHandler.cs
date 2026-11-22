using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorProfile;

public class GetSupervisorProfileQueryHandler : IRequestHandler<GetSupervisorProfileQuery, Result<SupervisorProfileResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetSupervisorProfileQueryHandler> _logger;

    public GetSupervisorProfileQueryHandler(
        IApplicationDbContext context,
        ILogger<GetSupervisorProfileQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<SupervisorProfileResponse>> Handle(
        GetSupervisorProfileQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var nowUtc = DateTime.UtcNow;
            var currentDate = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);
            var nextDate = currentDate.AddDays(1);

            var response = await _context.Supervisors
                .AsNoTracking()
                .Where(s => s.Id == request.SupervisorId)
                .Select(s => new SupervisorProfileResponse
                {
                    SupervisorId = s.Id,
                    FullName = s.FullName ?? string.Empty,
                    Email = s.Email ?? string.Empty,
                    PhoneNumber = s.PhoneNumber ?? string.Empty,
                    Address = s.Address,
                    DateOfBirth = null, // ApplicationUser doesn't have DateOfBirth

                    ClusterId = s.ClusterId,
                    ClusterName = s.ManagedCluster != null ? s.ManagedCluster.ClusterName : null,

                    TotalGroupsSupervised = s.SupervisedGroups.Count,
                    ActiveGroupsThisSeason = s.SupervisedGroups.Count(g =>
                        g.Status == GroupStatus.Active &&
                        g.YearSeason != null &&
                        g.YearSeason.StartDate < nextDate &&
                        g.YearSeason.EndDate >= currentDate),
                    CompletedPolygonTasks = _context.PlotPolygonTasks.Count(t =>
                        t.AssignedToSupervisorId == s.Id &&
                        t.Status == "Completed"),
                    PendingPolygonTasks = _context.PlotPolygonTasks.Count(t =>
                        t.AssignedToSupervisorId == s.Id &&
                        (t.Status == "Pending" || t.Status == "InProgress")),

                    CreatedAt = nowUtc, // Using current date as fallback
                    IsActive = true // Default to true
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (response == null)
            {
                _logger.LogWarning("Supervisor not found: {SupervisorId}", request.SupervisorId);
                return Result<SupervisorProfileResponse>.Failure(
                    "Supervisor not found.",
                    "NotFound"
                );
            }

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
