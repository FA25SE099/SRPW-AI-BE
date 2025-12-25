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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FormGroupsFromPreviewCommandHandler> _logger;
    private readonly WKTWriter _wktWriter;

    public FormGroupsFromPreviewCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<FormGroupsFromPreviewCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
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
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<FormGroupsResponse>.Failure($"Cluster {request.ClusterId} not found");
            }

            // Verify season exists
            var season = await _unitOfWork.Repository<Season>()
                .FindAsync(s => s.Id == request.SeasonId);

            if (season == null)
            {
                return Result<FormGroupsResponse>.Failure($"Season {request.SeasonId} not found");
            }

            // Get or verify YearSeason exists
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .FindAsync(ys => ys.ClusterId == request.ClusterId && 
                                ys.SeasonId == request.SeasonId && 
                                ys.Year == request.Year);

            if (yearSeason == null)
            {
                return Result<FormGroupsResponse>.Failure(
                    $"YearSeason not found for cluster {request.ClusterId}, season {request.SeasonId}, year {request.Year}. " +
                    "Please create a YearSeason first.");
            }

            // Check if groups already exist for this cluster/season/year
            var existingGroups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.ClusterId == request.ClusterId && 
                               g.YearSeasonId == yearSeason.Id && 
                               g.Year == request.Year);

            if (existingGroups.Any())
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
            var plots = await _unitOfWork.Repository<Plot>()
                .ListAsync(p => allPlotIds.Contains(p.Id));
            var plotsDict = plots.ToDictionary(p => p.Id);

            if (plotsDict.Count != allPlotIds.Count)
            {
                var missingPlots = allPlotIds.Except(plotsDict.Keys).ToList();
                return Result<FormGroupsResponse>.Failure(
                    $"Plot(s) not found: {string.Join(", ", missingPlots)}");
            }

            // Check for plots already grouped for THIS SEASON
            var alreadyGroupedPlots = new List<Guid>();
            foreach (var plotId in allPlotIds)
            {
                var isGroupedForSeason = await _unitOfWork.PlotRepository
                    .IsPlotAssignedToGroupForSeasonAsync(plotId, request.SeasonId, cancellationToken);
                if (isGroupedForSeason)
                {
                    alreadyGroupedPlots.Add(plotId);
                }
            }

            if (alreadyGroupedPlots.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    $"{alreadyGroupedPlots.Count} plot(s) are already assigned to a group for this season.");
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
            var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
                .ListAsync(rv => varietyIds.Contains(rv.Id));
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
                var supervisors = await _unitOfWork.SupervisorRepository
                    .ListAsync(s => supervisorIds.Contains(s.Id));
                
                if (supervisors.Count() != supervisorIds.Count)
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
                // Validate rice variety
                if (!varietyDict.ContainsKey(previewGroup.RiceVarietyId))
                {
                    return Result<FormGroupsResponse>.Failure(
                        $"Rice variety {previewGroup.RiceVarietyId} not found");
                }

                // Get plots for this group
                var groupPlots = previewGroup.PlotIds
                    .Select(id => plotsDict[id])
                    .ToList();

                // Calculate total area
                var totalArea = groupPlots.Sum(p => p.Area);

                // Calculate group boundary (union of plot boundaries)
                Polygon? groupBoundary = null;
                var plotBoundaries = groupPlots
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
                    YearSeasonId = yearSeason.Id,
                    Year = request.Year,
                    GroupName = previewGroup.GroupName,
                    PlantingDate = previewGroup.MedianPlantingDate,
                    Status = request.CreateGroupsImmediately ? GroupStatus.Active : GroupStatus.Draft,
                    TotalArea = totalArea,
                    Area = groupBoundary,
                    IsException = false
                };

                // Assign supervisor if specified
                if (previewGroup.SupervisorId.HasValue)
                {
                    group.SupervisorId = previewGroup.SupervisorId;
                }
                else
                {
                    warnings.Add($"Group '{previewGroup.GroupName ?? "unnamed"}' created without supervisor assignment");
                }

                // Add group
                await _unitOfWork.Repository<Group>().AddAsync(group);
                createdGroups.Add(group);

                // Assign plots to group
                foreach (var plot in groupPlots)
                {
                    var groupPlot = new GroupPlot
                    {
                        GroupId = group.Id,
                        PlotId = plot.Id
                    };
                    await _unitOfWork.Repository<GroupPlot>().AddAsync(groupPlot);
                }
            }

            // Save all changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                Groups = createdGroups.Select(g => new CreatedGroupDto
                {
                    GroupId = g.Id,
                    RiceVarietyId = g.YearSeason!.RiceVarietyId,
                    RiceVarietyName = varietyDict.GetValueOrDefault(g.YearSeason.RiceVarietyId)?.VarietyName ?? "Unknown",
                    SupervisorId = g.SupervisorId,
                    SupervisorName = g.SupervisorId.HasValue ? supervisorsForResponse.GetValueOrDefault(g.SupervisorId.Value) : null,
                    PlantingDate = g.PlantingDate!.Value,
                    PlantingWindowStart = g.PlantingDate!.Value,
                    PlantingWindowEnd = g.PlantingDate!.Value,
                    Status = g.Status.ToString(),
                    PlotCount = g.GroupPlots.Count,
                    TotalArea = g.TotalArea!.Value,
                    GroupBoundaryWkt = g.Area != null ? _wktWriter.Write(g.Area) : null,
                    PlotIds = g.GroupPlots.Select(gp => gp.PlotId).ToList()
                }).ToList(),
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
}

