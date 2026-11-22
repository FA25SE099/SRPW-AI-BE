using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;
using RiceProduction.Application.Common.Services;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using static RiceProduction.Application.Common.Services.GroupFormationService;

namespace RiceProduction.Application.GroupFeature.Commands.FormGroups;

public class FormGroupsCommandHandler : IRequestHandler<FormGroupsCommand, Result<FormGroupsResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FormGroupsCommandHandler> _logger;
    private readonly GroupFormationService _groupFormationService;
    private readonly WKTWriter _wktWriter;

    public FormGroupsCommandHandler(
        IApplicationDbContext context,
        ILogger<FormGroupsCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
        _groupFormationService = new GroupFormationService();
        _wktWriter = new WKTWriter();
    }

    public async Task<Result<FormGroupsResponse>> Handle(
        FormGroupsCommand request,
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

            // Build grouping parameters
            var parameters = new GroupingParameters
            {
                ProximityThreshold = request.ProximityThreshold ?? 2000,
                PlantingDateTolerance = request.PlantingDateTolerance ?? 2,
                MinGroupArea = request.MinGroupArea ?? 15.0m,
                MaxGroupArea = request.MaxGroupArea ?? 50.0m,
                MinPlotsPerGroup = request.MinPlotsPerGroup ?? 5,
                MaxPlotsPerGroup = request.MaxPlotsPerGroup ?? 15
            };

            // Get all farmers in cluster
            var farmerIds = await _context.Farmers
                .AsNoTracking()
                .Where(f => f.ClusterId == request.ClusterId)
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);

            // Get all plots for these farmers
            var plotsList = await _context.Plots
                .AsNoTracking()
                .Where(p => farmerIds.Contains(p.FarmerId))
                .ToListAsync(cancellationToken);
            var plotIds = plotsList.Select(p => p.Id).ToList();

            // Get plot cultivations for this season
            var cultivationsList = await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotIds.Contains(pc.PlotId) && 
                             pc.SeasonId == request.SeasonId)
                .ToListAsync(cancellationToken);

            if (!cultivationsList.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    "No plot cultivations found for this season. " +
                    "Farmers must select rice varieties before forming groups.");
            }

            // Find plots already grouped for THIS SEASON in one query
            // Business rule: A plot can belong to multiple groups, but only one group per season
            var alreadyGroupedPlotIds = (await _context.GroupPlots
                .AsNoTracking()
                .Where(gp => plotIds.Contains(gp.PlotId) &&
                             gp.Group.YearSeason != null &&
                             gp.Group.YearSeason.SeasonId == request.SeasonId)
                .Select(gp => gp.PlotId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

            // Build PlotClusterInfo list
            var plotDict = plotsList.ToDictionary(p => p.Id);
            var plotClusterInfos = new List<PlotClusterInfo>();

            foreach (var cultivation in cultivationsList)
            {
                if (!plotDict.TryGetValue(cultivation.PlotId, out var plot))
                    continue;

                if (alreadyGroupedPlotIds.Contains(plot.Id))
                    continue;

                var coordinate = plot.Coordinate ?? plot.Boundary?.Centroid;

                plotClusterInfos.Add(new PlotClusterInfo
                {
                    Plot = plot,
                    PlotCultivation = cultivation,
                    Coordinate = coordinate!,
                    PlantingDate = cultivation.PlantingDate.Date != DateTime.MinValue.Date ? cultivation.PlantingDate.Date : DateTime.UtcNow.Date,
                    RiceVarietyId = cultivation.RiceVarietyId,
                    IsGrouped = false
                });
            }

            if (!plotClusterInfos.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    "No eligible plots found for grouping. " +
                    "Ensure plots have coordinates/boundaries and aren't already grouped for this season.");
            }

            // Run grouping algorithm
            var (proposedGroups, ungroupedPlots) = _groupFormationService.FormGroups(
                plotClusterInfos,
                parameters
            );

            if (!proposedGroups.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    "No groups could be formed with current parameters. " +
                    "Try adjusting proximity threshold or minimum group size.");
            }

            // Get rice varieties
            var varietyIds = plotClusterInfos.Select(p => p.RiceVarietyId).Distinct().ToList();
            if (yearSeason.RiceVarietyId.HasValue && !varietyIds.Contains(yearSeason.RiceVarietyId.Value))
            {
                varietyIds.Add(yearSeason.RiceVarietyId.Value);
            }
            var riceVarieties = await _context.RiceVarieties
                .AsNoTracking()
                .Where(rv => varietyIds.Contains(rv.Id))
                .ToListAsync(cancellationToken);
            var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

            // Get supervisors if auto-assign
            List<Supervisor> availableSupervisors = new();
            if (request.AutoAssignSupervisors)
            {
                availableSupervisors = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => s.IsActive && s.CurrentFarmerCount < s.MaxFarmerCapacity)
                    .OrderBy(s => s.CurrentFarmerCount)
                    .ToListAsync(cancellationToken);
            }

            // Create groups
            var createdGroups = new List<Group>();
            var warnings = new List<string>();
            int supervisorIndex = 0;

            foreach (var proposedGroup in proposedGroups)
            {
                var group = new Group
                {
                    ClusterId = request.ClusterId,
                    YearSeasonId = yearSeason.Id,
                    Year = request.Year,
                    PlantingDate = proposedGroup.MedianPlantingDate,
                    Status = request.CreateGroupsImmediately ? GroupStatus.Active : GroupStatus.Draft,
                    TotalArea = proposedGroup.TotalArea,
                    Area = proposedGroup.GroupBoundary,
                    IsException = false
                };

                // Assign supervisor
                if (request.AutoAssignSupervisors && availableSupervisors.Any())
                {
                    var supervisor = availableSupervisors[supervisorIndex % availableSupervisors.Count];
                    group.SupervisorId = supervisor.Id;
                    supervisorIndex++;
                }
                else if (request.AutoAssignSupervisors)
                {
                    warnings.Add($"Group with {proposedGroup.Plots.Count} plots created without supervisor - no supervisors available");
                }

                // Add group
                _context.Groups.Add(group);
                createdGroups.Add(group);

                // Assign plots to group using many-to-many relationship
                foreach (var plotInfo in proposedGroup.Plots)
                {
                    var groupPlot = new GroupPlot
                    {
                        GroupId = group.Id,
                        PlotId = plotInfo.Plot.Id
                    };
                    _context.GroupPlots.Add(groupPlot);
                }
            }

            // Save all changes
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created {GroupCount} groups for cluster {ClusterId}, season {SeasonId}, year {Year}",
                createdGroups.Count, request.ClusterId, request.SeasonId, request.Year);

            // Build response
            var supervisorIds = createdGroups
                .Where(g => g.SupervisorId.HasValue)
                .Select(g => g.SupervisorId!.Value)
                .Distinct()
                .ToList();

            var supervisorsForResponse = new Dictionary<Guid, string>();
            if (supervisorIds.Any())
            {
                supervisorsForResponse = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => supervisorIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.FullName ?? string.Empty, cancellationToken);
            }

            var response = new FormGroupsResponse
            {
                ClusterId = request.ClusterId,
                SeasonId = request.SeasonId,
                Year = request.Year,
                GroupsCreated = createdGroups.Count,
                PlotsGrouped = createdGroups.Sum(g => g.GroupPlots.Count),
                UngroupedPlots = ungroupedPlots.Count,
                Groups = createdGroups
                    .Select(g => MapToCreatedGroupDto(g, yearSeason.RiceVarietyId, varietyDict, supervisorsForResponse))
                    .ToList(),
                UngroupedPlotIds = ungroupedPlots.Select(u => u.Plot.Plot.Id).ToList(),
                Warnings = warnings
            };

            if (ungroupedPlots.Any())
            {
                response.Warnings.Add($"{ungroupedPlots.Count} plots could not be grouped automatically and require manual assignment");
            }

            return Result<FormGroupsResponse>.Success(
                response,
                $"Successfully created {createdGroups.Count} groups with {response.PlotsGrouped} plots");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forming groups for cluster {ClusterId}", request.ClusterId);
            return Result<FormGroupsResponse>.Failure($"Error forming groups: {ex.Message}");
        }
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
            PlantingWindowStart = group.PlantingDate!.Value, // TODO: store actual window
            PlantingWindowEnd = group.PlantingDate!.Value,
            Status = group.Status.ToString(),
            PlotCount = group.GroupPlots.Count,
            TotalArea = group.TotalArea!.Value,
            GroupBoundaryWkt = group.Area != null ? _wktWriter.Write(group.Area) : null,
            PlotIds = group.GroupPlots.Select(gp => gp.PlotId).ToList()
        };
    }
}

