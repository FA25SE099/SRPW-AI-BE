using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.GroupFeature.Commands.FormGroups;

/// <summary>
/// PostGIS-optimized group formation command handler
/// Uses database-side spatial operations for improved accuracy and performance
/// </summary>
public class FormGroupsPostGISCommandHandler : IRequestHandler<FormGroupsCommand, Result<FormGroupsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FormGroupsPostGISCommandHandler> _logger;
    private readonly IPostGISGroupFormationService _postGISService;
    private readonly WKTWriter _wktWriter;

    public FormGroupsPostGISCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<FormGroupsPostGISCommandHandler> logger,
        IPostGISGroupFormationService postGISService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _postGISService = postGISService;
        _wktWriter = new WKTWriter();
    }

    public async Task<Result<FormGroupsResponse>> Handle(
        FormGroupsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting PostGIS-based group formation for cluster {ClusterId}, season {SeasonId}, year {Year}",
                request.ClusterId, request.SeasonId, request.Year);

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

            // Build grouping parameters
            var parameters = new PostGISGroupingParameters
            {
                ProximityThreshold = request.ProximityThreshold ?? 100, // 100m default
                PlantingDateTolerance = request.PlantingDateTolerance ?? 2,
                MinGroupArea = request.MinGroupArea ?? 5.0m,
                MaxGroupArea = request.MaxGroupArea ?? 50.0m,
                MinPlotsPerGroup = request.MinPlotsPerGroup ?? 3,
                MaxPlotsPerGroup = request.MaxPlotsPerGroup ?? 10,
                BorderBuffer = 10
            };

            _logger.LogInformation("Using PostGIS spatial clustering with parameters: Proximity={Proximity}m, DateTolerance={DateTolerance}days",
                parameters.ProximityThreshold, parameters.PlantingDateTolerance);

            // Run PostGIS-based grouping algorithm with cluster and season filters
            var groupingResult = await _postGISService.FormGroupsAsync(
                parameters, 
                request.ClusterId, 
                request.SeasonId, 
                cancellationToken);

            if (!groupingResult.Groups.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    "No groups could be formed with current parameters. " +
                    "Try adjusting proximity threshold or minimum group size.");
            }

            _logger.LogInformation("PostGIS clustering completed: {GroupCount} groups formed, {UngroupedCount} plots ungrouped",
                groupingResult.Groups.Count, groupingResult.UngroupedPlots.Count);

            // Get rice varieties for the groups
            var varietyIds = groupingResult.Groups.Select(g => g.RiceVarietyId).Distinct().ToList();
            var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
                .ListAsync(rv => varietyIds.Contains(rv.Id));
            var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

            // Get supervisors if auto-assign
            List<Supervisor> availableSupervisors = new();
            if (request.AutoAssignSupervisors)
            {
                var supervisors = await _unitOfWork.SupervisorRepository
                    .ListAsync(s => s.IsActive);
                availableSupervisors = supervisors
                    .Where(s => s.CurrentFarmerCount < s.MaxFarmerCapacity)
                    .OrderBy(s => s.CurrentFarmerCount)
                    .ToList();
            }

            // Create groups from PostGIS results
            var createdGroups = new List<Group>();
            var warnings = new List<string>();
            int supervisorIndex = 0;

            foreach (var proposedGroup in groupingResult.Groups)
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
                    warnings.Add($"Group with {proposedGroup.PlotCount} plots created without supervisor - no supervisors available");
                }

                // Add group
                await _unitOfWork.Repository<Group>().AddAsync(group);
                createdGroups.Add(group);

                // Assign plots to group using many-to-many relationship
                foreach (var plotId in proposedGroup.PlotIds)
                {
                    var groupPlot = new GroupPlot
                    {
                        GroupId = group.Id,
                        PlotId = plotId
                    };
                    await _unitOfWork.Repository<GroupPlot>().AddAsync(groupPlot);
                }
            }

            // Save all changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                var supervisorsList = await _unitOfWork.SupervisorRepository
                    .ListAsync(s => supervisorIds.Contains(s.Id));
                supervisorsForResponse = supervisorsList.ToDictionary(s => s.Id, s => s.FullName ?? string.Empty);
            }

            var totalPlotsGrouped = createdGroups.Sum(g => g.GroupPlots.Count);

            var response = new FormGroupsResponse
            {
                ClusterId = request.ClusterId,
                SeasonId = request.SeasonId,
                Year = request.Year,
                GroupsCreated = createdGroups.Count,
                PlotsGrouped = totalPlotsGrouped,
                UngroupedPlots = groupingResult.UngroupedPlots.Count,
                Groups = createdGroups.Select(g =>
                {
                    var proposedGroup = groupingResult.Groups.First(pg => pg.PlotIds.SequenceEqual(g.GroupPlots.Select(gp => gp.PlotId)));
                    return new CreatedGroupDto
                    {
                        GroupId = g.Id,
                        RiceVarietyId = (Guid)(g.YearSeason?.RiceVarietyId),
                        RiceVarietyName = g.YearSeason?.RiceVarietyId != null && g.YearSeason.RiceVarietyId.HasValue 
                            ? varietyDict.GetValueOrDefault(g.YearSeason.RiceVarietyId.Value)?.VarietyName ?? "Unknown" 
                            : "Not Set",
                        SupervisorId = g.SupervisorId,
                        SupervisorName = g.SupervisorId.HasValue ? supervisorsForResponse.GetValueOrDefault(g.SupervisorId.Value) : null,
                        PlantingDate = g.PlantingDate!.Value,
                        PlantingWindowStart = proposedGroup.PlantingWindowStart,
                        PlantingWindowEnd = proposedGroup.PlantingWindowEnd,
                        Status = g.Status.ToString(),
                        PlotCount = g.GroupPlots.Count,
                        TotalArea = g.TotalArea!.Value,
                        GroupBoundaryWkt = g.Area != null ? _wktWriter.Write(g.Area) : null,
                        PlotIds = g.GroupPlots.Select(gp => gp.PlotId).ToList()
                    };
                }).ToList(),
                UngroupedPlotIds = groupingResult.UngroupedPlots.Select(u => u.PlotId).ToList(),
                Warnings = warnings
            };

            // Add detailed ungrouped plot information
            if (groupingResult.UngroupedPlots.Any())
            {
                var ungroupedSummary = groupingResult.UngroupedPlots
                    .GroupBy(u => u.UngroupedReason)
                    .Select(g => $"{g.Key}: {g.Count()} plots")
                    .ToList();

                response.Warnings.Add($"{groupingResult.UngroupedPlots.Count} plots could not be grouped: {string.Join(", ", ungroupedSummary)}");

                _logger.LogWarning("Ungrouped plots breakdown: {UngroupedBreakdown}",
                    string.Join(", ", ungroupedSummary));
            }

            return Result<FormGroupsResponse>.Success(
                response,
                $"Successfully created {createdGroups.Count} groups with {totalPlotsGrouped} plots using PostGIS spatial clustering");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forming groups for cluster {ClusterId} using PostGIS", request.ClusterId);
            return Result<FormGroupsResponse>.Failure($"Error forming groups: {ex.Message}");
        }
    }
}

