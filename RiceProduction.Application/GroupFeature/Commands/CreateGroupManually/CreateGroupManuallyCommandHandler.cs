using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.GroupFeature.Commands.CreateGroupManually;

public class CreateGroupManuallyCommandHandler : IRequestHandler<CreateGroupManuallyCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateGroupManuallyCommandHandler> _logger;

    public CreateGroupManuallyCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateGroupManuallyCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        CreateGroupManuallyCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate cluster exists
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<Guid>.Failure($"Cluster {request.ClusterId} not found");
            }

            // Validate season exists
            var season = await _unitOfWork.Repository<Season>()
                .FindAsync(s => s.Id == request.SeasonId);

            if (season == null)
            {
                return Result<Guid>.Failure($"Season {request.SeasonId} not found");
            }

            // Validate rice variety exists
            var riceVariety = await _unitOfWork.Repository<RiceVariety>()
                .FindAsync(rv => rv.Id == request.RiceVarietyId);

            if (riceVariety == null)
            {
                return Result<Guid>.Failure($"Rice variety {request.RiceVarietyId} not found");
            }

            // Validate supervisor if provided
            if (request.SupervisorId.HasValue)
            {
                var supervisors = await _unitOfWork.SupervisorRepository
                    .ListAsync(s => s.Id == request.SupervisorId.Value);
                var supervisor = supervisors.FirstOrDefault();

                if (supervisor == null)
                {
                    return Result<Guid>.Failure($"Supervisor {request.SupervisorId.Value} not found");
                }

                if (!supervisor.IsActive)
                {
                    return Result<Guid>.Failure("Supervisor is not active");
                }
            }

            // Validate plots exist
            if (!request.PlotIds.Any())
            {
                return Result<Guid>.Failure("At least one plot is required");
            }

            var plots = await _unitOfWork.Repository<Plot>()
                .ListAsync(p => request.PlotIds.Contains(p.Id));
            var plotsList = plots.ToList();

            if (plotsList.Count != request.PlotIds.Count)
            {
                return Result<Guid>.Failure("One or more plots not found");
            }

            // Check if any plot is already in a group for THIS SEASON
            // Business rule: A plot can belong to multiple groups, but only one group per season
            var alreadyGrouped = new List<Plot>();
            foreach (var plot in plotsList)
            {
                var isGroupedForSeason = await _unitOfWork.PlotRepository.IsPlotAssignedToGroupForSeasonAsync(plot.Id, request.SeasonId, cancellationToken);
                if (isGroupedForSeason)
                {
                    alreadyGrouped.Add(plot);
                }
            }
            if (alreadyGrouped.Any())
            {
                return Result<Guid>.Failure(
                    $"{alreadyGrouped.Count} plot(s) are already assigned to a group for this season. " +
                    "A plot can only belong to one group per season. Remove them from existing groups for this season first.");
            }

            // Calculate total area and union of boundaries
            var totalArea = plotsList.Sum(p => p.Area);
            Polygon? groupBoundary = null;

            var plotBoundaries = plotsList
                .Where(p => p.Boundary != null)
                .Select(p => p.Boundary!)
                .ToList();

            if (plotBoundaries.Any())
            {
                Geometry union = plotBoundaries.First();
                foreach (var boundary in plotBoundaries.Skip(1))
                {
                    union = union.Union(boundary);
                }

                if (union is Polygon polygon)
                {
                    groupBoundary = polygon;
                }
                else if (union is MultiPolygon multiPolygon)
                {
                    groupBoundary = (Polygon)multiPolygon.ConvexHull();
                }
            }

            // Create group
            var group = new Group
            {
                ClusterId = request.ClusterId,
                SupervisorId = request.SupervisorId,
                RiceVarietyId = request.RiceVarietyId,
                SeasonId = request.SeasonId,
                Year = request.Year,
                PlantingDate = request.PlantingDate,
                Status = GroupStatus.Draft,
                IsException = request.IsException,
                ExceptionReason = request.ExceptionReason,
                TotalArea = totalArea,
                Area = groupBoundary
            };

            await _unitOfWork.Repository<Group>().AddAsync(group);

            // Assign plots to group using many-to-many relationship
            foreach (var plot in plotsList)
            {
                var groupPlot = new GroupPlot
                {
                    GroupId = group.Id,
                    PlotId = plot.Id
                };
                await _unitOfWork.Repository<GroupPlot>().AddAsync(groupPlot);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Manually created group {GroupId} for cluster {ClusterId} with {PlotCount} plots, total area {Area} ha",
                group.Id, request.ClusterId, plotsList.Count, totalArea);

            return Result<Guid>.Success(
                group.Id,
                $"Successfully created group with {plotsList.Count} plots ({totalArea:F2} ha)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual group for cluster {ClusterId}", request.ClusterId);
            return Result<Guid>.Failure($"Error creating group: {ex.Message}");
        }
    }
}

