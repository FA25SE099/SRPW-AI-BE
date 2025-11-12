using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetPolygonAssignmentTasks;

public class GetPolygonAssignmentTasksQueryHandler 
    : IRequestHandler<GetPolygonAssignmentTasksQuery, Result<List<PlotPolygonTaskDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPolygonAssignmentTasksQueryHandler> _logger;

    public GetPolygonAssignmentTasksQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPolygonAssignmentTasksQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<PlotPolygonTaskDto>>> Handle(
        GetPolygonAssignmentTasksQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Build query for polygon tasks
            var query = _unitOfWork.Repository<PlotPolygonTask>()
                .GetQueryable()
                .Where(t => t.AssignedToSupervisorId == request.SupervisorId);

            // Filter by status if provided
            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(t => t.Status == request.Status);
            }

            // Get tasks with related plot and farmer data
            var tasks = await query
                .Include(t => t.Plot)
                .ThenInclude(p => p.Farmer)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.AssignedAt)
                .ToListAsync(cancellationToken);

            var taskDtos = tasks.Select(t => new PlotPolygonTaskDto
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
                FarmerName = t.Plot.Farmer?.FullName,
                FarmerPhone = t.Plot.Farmer?.PhoneNumber
            }).ToList();

            return Result<List<PlotPolygonTaskDto>>.Success(taskDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting polygon assignment tasks for supervisor {SupervisorId}", request.SupervisorId);
            return Result<List<PlotPolygonTaskDto>>.Failure($"Error retrieving tasks: {ex.Message}");
        }
    }
}

