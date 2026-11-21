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

            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);
            if (cluster == null)
            {
                return Result<FormGroupsResponse>.Failure($"Cluster {request.ClusterId} not found");
            }

            var season = await _unitOfWork.Repository<Season>()
                .FindAsync(s => s.Id == request.SeasonId);
            if (season == null)
            {
                return Result<FormGroupsResponse>.Failure($"Season {request.SeasonId} not found");
            }

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

            var groupRepo = _unitOfWork.Repository<Group>();
            var existingGroups = await groupRepo.ListAsync(g =>
                g.ClusterId == request.ClusterId &&
                g.YearSeasonId == yearSeason.Id &&
                g.Year == request.Year);

            if (existingGroups.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    $"Groups already exist for this cluster in {season.SeasonName} {request.Year}. " +
                    "Please disband existing groups before forming new ones.");
            }

            var parameters = BuildGroupingParameters(request);
            _logger.LogInformation("Using PostGIS spatial clustering with parameters: Proximity={Proximity}m, DateTolerance={DateTolerance}days",
                parameters.ProximityThreshold, parameters.PlantingDateTolerance);

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

            var varietyIds = groupingResult.Groups.Select(g => g.RiceVarietyId).Distinct().ToList();
            var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
                .ListAsync(rv => varietyIds.Contains(rv.Id));
            var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

            var availableSupervisors = await GetAvailableSupervisorsAsync(request.AutoAssignSupervisors);

            var groupPlotRepo = _unitOfWork.Repository<GroupPlot>();
            var createdGroups = new List<Group>();
            var warnings = new List<string>();
            var supervisorIndex = 0;

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

                if (request.AutoAssignSupervisors)
                {
                    if (availableSupervisors.Any())
                    {
                        var supervisor = availableSupervisors[supervisorIndex % availableSupervisors.Count];
                        group.SupervisorId = supervisor.Id;
                        supervisorIndex++;
                    }
                    else
                    {
                        warnings.Add($"Group with {proposedGroup.PlotCount} plots created without supervisor - no supervisors available");
                    }
                }

                await groupRepo.AddAsync(group);
                createdGroups.Add(group);

                foreach (var plotId in proposedGroup.PlotIds)
                {
                    await groupPlotRepo.AddAsync(new GroupPlot
                    {
                        GroupId = group.Id,
                        PlotId = plotId
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created {GroupCount} groups for cluster {ClusterId}, season {SeasonId}, year {Year}",
                createdGroups.Count, request.ClusterId, request.SeasonId, request.Year);

            var supervisorsForResponse = await GetSupervisorNamesAsync(createdGroups);
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
                    var riceVarietyName = g.YearSeason?.RiceVarietyId is Guid varietyId
                        ? varietyDict.GetValueOrDefault(varietyId)?.VarietyName ?? "Unknown"
                        : "Not Set";

                    return new CreatedGroupDto
                    {
                        GroupId = g.Id,
                        RiceVarietyId = (Guid)(g.YearSeason?.RiceVarietyId),
                        RiceVarietyName = riceVarietyName,
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

            if (groupingResult.UngroupedPlots.Any())
            {
                var ungroupedSummary = groupingResult.UngroupedPlots
                    .GroupBy(u => u.UngroupedReason)
                    .Select(g => $"{g.Key}: {g.Count()} plots")
                    .ToList();

                var summaryText = string.Join(", ", ungroupedSummary);
                response.Warnings.Add($"{groupingResult.UngroupedPlots.Count} plots could not be grouped: {summaryText}");
                _logger.LogWarning("Ungrouped plots breakdown: {UngroupedBreakdown}", summaryText);
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

    private static PostGISGroupingParameters BuildGroupingParameters(FormGroupsCommand request) =>
        new()
        {
            ProximityThreshold = request.ProximityThreshold ?? 100, // 100m default
            PlantingDateTolerance = request.PlantingDateTolerance ?? 2,
            MinGroupArea = request.MinGroupArea ?? 5.0m,
            MaxGroupArea = request.MaxGroupArea ?? 50.0m,
            MinPlotsPerGroup = request.MinPlotsPerGroup ?? 3,
            MaxPlotsPerGroup = request.MaxPlotsPerGroup ?? 10,
            BorderBuffer = 10
        };

    private async Task<List<Supervisor>> GetAvailableSupervisorsAsync(bool autoAssignSupervisors)
    {
        if (!autoAssignSupervisors)
        {
            return new List<Supervisor>();
        }

        var supervisors = await _unitOfWork.SupervisorRepository.ListAsync(s => s.IsActive);
        return supervisors
            .Where(s => s.CurrentFarmerCount < s.MaxFarmerCapacity)
            .OrderBy(s => s.CurrentFarmerCount)
            .ToList();
    }

    private async Task<Dictionary<Guid, string>> GetSupervisorNamesAsync(List<Group> createdGroups)
    {
        var supervisorIds = createdGroups
            .Where(g => g.SupervisorId.HasValue)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToList();

        if (!supervisorIds.Any())
        {
            return new Dictionary<Guid, string>();
        }

        var supervisorsList = await _unitOfWork.SupervisorRepository
            .ListAsync(s => supervisorIds.Contains(s.Id));
        return supervisorsList.ToDictionary(s => s.Id, s => s.FullName ?? string.Empty);
    }
}

