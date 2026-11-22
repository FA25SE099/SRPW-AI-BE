using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.GroupFeature.Commands.FormGroupsFromPreview;

public class FormGroupsFromPreviewCommandHandler : IRequestHandler<FormGroupsFromPreviewCommand, Result<FormGroupsResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FormGroupsFromPreviewCommandHandler> _logger;
    private readonly WKTWriter _wktWriter;

    public FormGroupsFromPreviewCommandHandler(
        IApplicationDbContext context,
        ILogger<FormGroupsFromPreviewCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
        _wktWriter = new WKTWriter();
    }

    public async Task<Result<FormGroupsResponse>> Handle(
        FormGroupsFromPreviewCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify cluster exists
            var clusterExists = await _context.Clusters
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.ClusterId, cancellationToken);

            if (!clusterExists)
            {
                return Result<FormGroupsResponse>.Failure($"Cluster {request.ClusterId} not found");
            }

            // Verify season exists
            var season = await _context.Seasons
                .AsNoTracking()
                .Where(s => s.Id == request.SeasonId)
                .Select(s => new { s.Id, s.SeasonName })
                .FirstOrDefaultAsync(cancellationToken);

            if (season == null)
            {
                return Result<FormGroupsResponse>.Failure($"Season {request.SeasonId} not found");
            }

            // Get or verify YearSeason exists
            var yearSeason = await _context.YearSeasons
                .AsNoTracking()
                .FirstOrDefaultAsync(ys => ys.ClusterId == request.ClusterId && 
                                           ys.SeasonId == request.SeasonId && 
                                           ys.Year == request.Year,
                    cancellationToken);

            if (yearSeason == null)
            {
                return Result<FormGroupsResponse>.Failure(
                    $"YearSeason not found for cluster {request.ClusterId}, season {request.SeasonId}, year {request.Year}. " +
                    "Please create a YearSeason first.");
            }

            // Check if groups already exist for this cluster/season/year
            var existingGroupsExist = await _context.Groups
                .AsNoTracking()
                .AnyAsync(g => g.ClusterId == request.ClusterId && 
                               g.YearSeasonId == yearSeason.Id && 
                               g.Year == request.Year,
                    cancellationToken);

            if (existingGroupsExist)
            {
                return Result<FormGroupsResponse>.Failure(
                    $"Groups already exist for this cluster in {season.SeasonName} {request.Year}. " +
                    "Please disband existing groups before forming new ones.");
            }

            if (!request.Groups.Any())
            {
                return Result<FormGroupsResponse>.Failure("No groups provided to create");
            }

            // Collect all plot IDs to validate
            var allPlotIds = request.Groups.SelectMany(g => g.PlotIds).Distinct().ToList();
            
            // Verify all plots exist
            var plots = await _context.Plots
                .AsNoTracking()
                .Where(p => allPlotIds.Contains(p.Id))
                .ToListAsync(cancellationToken);
            var plotsDict = plots.ToDictionary(p => p.Id);

            if (plotsDict.Count != allPlotIds.Count)
            {
                var missingPlots = allPlotIds.Except(plotsDict.Keys).ToList();
                return Result<FormGroupsResponse>.Failure(
                    $"Plot(s) not found: {string.Join(", ", missingPlots)}");
            }

            // Check for plots already grouped for THIS SEASON
            var alreadyGroupedPlotCount = await _context.GroupPlots
                .AsNoTracking()
                .Where(gp => allPlotIds.Contains(gp.PlotId) &&
                             gp.Group.YearSeason != null &&
                             gp.Group.YearSeason.SeasonId == request.SeasonId)
                .Select(gp => gp.PlotId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (alreadyGroupedPlotCount > 0)
            {
                return Result<FormGroupsResponse>.Failure(
                    $"{alreadyGroupedPlotCount} plot(s) are already assigned to a group for this season.");
            }

            // Check for duplicate plot assignments
            var plotIdCounts = request.Groups
                .SelectMany(g => g.PlotIds)
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (plotIdCounts.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    $"Plot(s) assigned to multiple groups: {string.Join(", ", plotIdCounts)}");
            }

            // Get rice varieties for validation
            var varietyIds = request.Groups.Select(g => g.RiceVarietyId).Distinct().ToList();
            if (yearSeason.RiceVarietyId.HasValue && !varietyIds.Contains(yearSeason.RiceVarietyId.Value))
            {
                varietyIds.Add(yearSeason.RiceVarietyId.Value);
            }
            var riceVarieties = await _context.RiceVarieties
                .AsNoTracking()
                .Where(rv => varietyIds.Contains(rv.Id))
                .ToListAsync(cancellationToken);
            var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

            // Validate supervisors if specified
            var supervisorIds = request.Groups
                .Where(g => g.SupervisorId.HasValue)
                .Select(g => g.SupervisorId!.Value)
                .Distinct()
                .ToList();

            Dictionary<Guid, string> supervisorsForResponse = new();
            if (supervisorIds.Any())
            {
                var supervisors = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => supervisorIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.FullName })
                    .ToListAsync(cancellationToken);

                if (supervisors.Count != supervisorIds.Count)
                {
                    var missingSupervisors = supervisorIds.Except(supervisors.Select(s => s.Id)).ToList();
                    return Result<FormGroupsResponse>.Failure(
                        $"Supervisor(s) not found: {string.Join(", ", missingSupervisors)}");
                }

                supervisorsForResponse = supervisors.ToDictionary(s => s.Id, s => s.FullName ?? "Unknown");
            }

            // Create groups from preview
            var createdGroups = new List<Group>();
            var warnings = new List<string>();

            foreach (var previewGroup in request.Groups)
            {
                if (!varietyDict.ContainsKey(previewGroup.RiceVarietyId))
                {
                    return Result<FormGroupsResponse>.Failure(
                        $"Rice variety {previewGroup.RiceVarietyId} not found");
                }

                var groupPlots = previewGroup.PlotIds
                    .Select(id => plotsDict[id])
                    .ToList();

                var group = new Group
                {
                    ClusterId = request.ClusterId,
                    YearSeasonId = yearSeason.Id,
                    Year = request.Year,
                    GroupName = previewGroup.GroupName,
                    PlantingDate = previewGroup.MedianPlantingDate,
                    Status = request.CreateGroupsImmediately ? GroupStatus.Active : GroupStatus.Draft,
                    TotalArea = groupPlots.Sum(p => p.Area),
                    Area = CalculateGroupBoundary(groupPlots),
                    IsException = false
                };

                if (previewGroup.SupervisorId.HasValue)
                {
                    group.SupervisorId = previewGroup.SupervisorId;
                }
                else
                {
                    warnings.Add($"Group '{previewGroup.GroupName ?? "unnamed"}' created without supervisor assignment");
                }

                _context.Groups.Add(group);
                createdGroups.Add(group);

                foreach (var plot in groupPlots)
                {
                    var groupPlot = new GroupPlot
                    {
                        GroupId = group.Id,
                        PlotId = plot.Id
                    };
                    _context.GroupPlots.Add(groupPlot);
                }
            }

            // Save all changes
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created {GroupCount} groups from preview for cluster {ClusterId}, season {SeasonId}, year {Year}",
                createdGroups.Count, request.ClusterId, request.SeasonId, request.Year);

            var totalPlotsGrouped = createdGroups.Sum(g => g.GroupPlots.Count);

            var response = new FormGroupsResponse
            {
                ClusterId = request.ClusterId,
                SeasonId = request.SeasonId,
                Year = request.Year,
                GroupsCreated = createdGroups.Count,
                PlotsGrouped = totalPlotsGrouped,
                UngroupedPlots = 0, // No ungrouped plots since we're using exact preview
                Groups = createdGroups
                    .Select(g => MapToCreatedGroupDto(g, yearSeason.RiceVarietyId, varietyDict, supervisorsForResponse))
                    .ToList(),
                UngroupedPlotIds = new List<Guid>(),
                Warnings = warnings
            };

            return Result<FormGroupsResponse>.Success(
                response,
                $"Successfully created {createdGroups.Count} groups from preview with {totalPlotsGrouped} plots");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forming groups from preview for cluster {ClusterId}", request.ClusterId);
            return Result<FormGroupsResponse>.Failure($"Error forming groups from preview: {ex.Message}");
        }
    }

    private static Polygon? CalculateGroupBoundary(List<Plot> groupPlots)
    {
        var plotBoundaries = groupPlots
            .Where(p => p.Boundary != null)
            .Select(p => p.Boundary!)
            .ToList();

        if (plotBoundaries.Count == 0)
            return null;

        Geometry union = plotBoundaries[0];
        foreach (var boundary in plotBoundaries.Skip(1))
        {
            union = union.Union(boundary);
        }

        return union switch
        {
            Polygon polygon => polygon,
            MultiPolygon multiPolygon => (Polygon)multiPolygon.ConvexHull(),
            _ => null
        };
    }

    private CreatedGroupDto MapToCreatedGroupDto(
        Group group,
        Guid? yearSeasonRiceVarietyId,
        Dictionary<Guid, RiceVariety> varietyDict,
        Dictionary<Guid, string> supervisorsForResponse)
    {
        var effectiveVarietyId = group.YearSeason?.RiceVarietyId ?? yearSeasonRiceVarietyId;
        return new CreatedGroupDto
        {
            GroupId = group.Id,
            RiceVarietyId = effectiveVarietyId ?? Guid.Empty,
            RiceVarietyName = effectiveVarietyId is Guid riceVarietyId
                ? varietyDict.GetValueOrDefault(riceVarietyId)?.VarietyName ?? "Unknown"
                : "Not Set",
            SupervisorId = group.SupervisorId,
            SupervisorName = group.SupervisorId.HasValue
                ? supervisorsForResponse.GetValueOrDefault(group.SupervisorId.Value)
                : null,
            PlantingDate = group.PlantingDate!.Value,
            PlantingWindowStart = group.PlantingDate!.Value,
            PlantingWindowEnd = group.PlantingDate!.Value,
            Status = group.Status.ToString(),
            PlotCount = group.GroupPlots.Count,
            TotalArea = group.TotalArea!.Value,
            GroupBoundaryWkt = group.Area != null ? _wktWriter.Write(group.Area) : null,
            PlotIds = group.GroupPlots.Select(gp => gp.PlotId).ToList()
        };
    }
}

