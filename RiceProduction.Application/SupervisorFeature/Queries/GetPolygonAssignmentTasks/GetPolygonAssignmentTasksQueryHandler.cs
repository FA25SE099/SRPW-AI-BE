using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetPolygonAssignmentTasks;

public class GetPolygonAssignmentTasksQueryHandler 
    : IRequestHandler<GetPolygonAssignmentTasksQuery, Result<List<PlotPolygonTaskDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPolygonAssignmentTasksQueryHandler> _logger;

    public GetPolygonAssignmentTasksQueryHandler(
        IApplicationDbContext context,
        ILogger<GetPolygonAssignmentTasksQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<PlotPolygonTaskDto>>> Handle(
        GetPolygonAssignmentTasksQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.PlotPolygonTasks
                .AsNoTracking()
                .Where(t => t.AssignedToSupervisorId == request.SupervisorId);

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(t => t.Status == request.Status);
            }

            var taskDtos = await query
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.AssignedAt)
                .Select(t => new PlotPolygonTaskDto
                {
                    Id = t.Id,
                    PlotId = t.PlotId,
                    Status = t.Status,
                    AssignedAt = t.AssignedAt,
                    CompletedAt = t.CompletedAt,
                    Notes = t.Notes,
                    Priority = t.Priority,
                    SoThua = t.Plot.SoThua,
                    SoTo = t.Plot.SoTo,
                    PlotArea = t.Plot.Area,
                    SoilType = t.Plot.SoilType,
                    FarmerId = t.Plot.FarmerId,
                    FarmerName = t.Plot.Farmer != null ? t.Plot.Farmer.FullName : null,
                    FarmerPhone = t.Plot.Farmer != null ? t.Plot.Farmer.PhoneNumber : null
                })
                .ToListAsync(cancellationToken);

            return Result<List<PlotPolygonTaskDto>>.Success(taskDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting polygon assignment tasks for supervisor {SupervisorId}", request.SupervisorId);
            return Result<List<PlotPolygonTaskDto>>.Failure($"Error retrieving tasks: {ex.Message}");
        }
    }
}

