using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.GroupFeature.Commands.CreateGroupManually;

public class CreateGroupManuallyCommandHandler : IRequestHandler<CreateGroupManuallyCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateGroupManuallyCommandHandler> _logger;

    public CreateGroupManuallyCommandHandler(
        IApplicationDbContext context,
        ILogger<CreateGroupManuallyCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        CreateGroupManuallyCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate cluster exists
            var clusterExists = await _context.Clusters
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.ClusterId, cancellationToken);

            if (!clusterExists)
            {
                return Result<Guid>.Failure($"Cluster {request.ClusterId} not found");
            }

            // Validate season exists
            var seasonExists = await _context.Seasons
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.SeasonId, cancellationToken);

            if (!seasonExists)
            {
                return Result<Guid>.Failure($"Season {request.SeasonId} not found");
            }

            // Validate rice variety exists
            var riceVarietyExists = await _context.RiceVarieties
                .AsNoTracking()
                .AnyAsync(rv => rv.Id == request.RiceVarietyId, cancellationToken);

            if (!riceVarietyExists)
            {
                return Result<Guid>.Failure($"Rice variety {request.RiceVarietyId} not found");
            }

            // Validate supervisor if provided
            if (request.SupervisorId.HasValue)
            {
                var supervisor = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => s.Id == request.SupervisorId.Value)
                    .Select(s => new { s.Id, s.IsActive })
                    .FirstOrDefaultAsync(cancellationToken);

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

            var plotsList = await _context.Plots
                .AsNoTracking()
                .Where(p => request.PlotIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            if (plotsList.Count != request.PlotIds.Count)
            {
                return Result<Guid>.Failure("One or more plots not found");
            }

            // Check if any plot is already in a group for THIS SEASON
            // Business rule: A plot can belong to multiple groups, but only one group per season
            var plotIds = plotsList.Select(p => p.Id).ToList();
            var alreadyGroupedCount = await _context.GroupPlots
                .AsNoTracking()
                .Where(gp => plotIds.Contains(gp.PlotId) &&
                             gp.Group.YearSeason != null &&
                             gp.Group.YearSeason.SeasonId == request.SeasonId)
                .Select(gp => gp.PlotId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (alreadyGroupedCount > 0)
            {
                return Result<Guid>.Failure(
                    $"{alreadyGroupedCount} plot(s) are already assigned to a group for this season. " +
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

            // Get or verify YearSeason exists
            var yearSeason = await _context.YearSeasons
                .AsNoTracking()
                .Where(ys => ys.ClusterId == request.ClusterId && 
                             ys.SeasonId == request.SeasonId && 
                             ys.Year == request.Year)
                .Select(ys => new { ys.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (yearSeason == null)
            {
                return Result<Guid>.Failure(
                    $"YearSeason not found for cluster {request.ClusterId}, season {request.SeasonId}, year {request.Year}. " +
                    "Please create a YearSeason first.");
            }

            // Create group
            var group = new Group
            {
                ClusterId = request.ClusterId,
                SupervisorId = request.SupervisorId,
                YearSeasonId = yearSeason.Id,
                Year = request.Year,
                PlantingDate = request.PlantingDate,
                Status = GroupStatus.Draft,
                IsException = request.IsException,
                ExceptionReason = request.ExceptionReason,
                TotalArea = totalArea,
                Area = groupBoundary
            };

            _context.Groups.Add(group);

            // Assign plots to group using many-to-many relationship
            foreach (var plot in plotsList)
            {
                var groupPlot = new GroupPlot
                {
                    GroupId = group.Id,
                    PlotId = plot.Id
                };
                _context.GroupPlots.Add(groupPlot);
            }

            await _context.SaveChangesAsync(cancellationToken);

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

