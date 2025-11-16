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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FormGroupsCommandHandler> _logger;
    private readonly GroupFormationService _groupFormationService;
    private readonly WKTWriter _wktWriter;

    public FormGroupsCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<FormGroupsCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
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

            // Check if groups already exist for this cluster/season/year
            var existingGroups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.ClusterId == request.ClusterId && 
                               g.SeasonId == request.SeasonId && 
                               g.Year == request.Year);

            if (existingGroups.Any())
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
            var farmers = await _unitOfWork.FarmerRepository
                .ListAsync(f => f.ClusterId == request.ClusterId);
            var farmersList = farmers.ToList();
            var farmerIds = farmersList.Select(f => f.Id).ToList();

            // Get all plots for these farmers
            var plots = await _unitOfWork.Repository<Plot>()
                .ListAsync(p => farmerIds.Contains(p.FarmerId));
            var plotsList = plots.ToList();
            var plotIds = plotsList.Select(p => p.Id).ToList();

            // Get plot cultivations for this season
            var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
                .ListAsync(pc => plotIds.Contains(pc.PlotId) && 
                                pc.SeasonId == request.SeasonId);
            var cultivationsList = plotCultivations.ToList();

            if (!cultivationsList.Any())
            {
                return Result<FormGroupsResponse>.Failure(
                    "No plot cultivations found for this season. " +
                    "Farmers must select rice varieties before forming groups.");
            }

            // Build PlotClusterInfo list
            var plotClusterInfos = new List<PlotClusterInfo>();

            foreach (var cultivation in cultivationsList)
            {
                var plot = plotsList.FirstOrDefault(p => p.Id == cultivation.PlotId);
                if (plot == null) continue;

                // Skip already grouped plots
                if (plot.GroupId.HasValue)
                    continue;

                var coordinate = plot.Coordinate ?? (plot.Boundary != null ? plot.Boundary.Centroid : null);

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
                    "Ensure plots have coordinates/boundaries and aren't already grouped.");
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

            // Create groups
            var createdGroups = new List<Group>();
            var warnings = new List<string>();
            int supervisorIndex = 0;

            foreach (var proposedGroup in proposedGroups)
            {
                var group = new Group
                {
                    ClusterId = request.ClusterId,
                    RiceVarietyId = proposedGroup.RiceVarietyId,
                    SeasonId = request.SeasonId,
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
                await _unitOfWork.Repository<Group>().AddAsync(group);
                createdGroups.Add(group);

                // Assign plots to group
                foreach (var plotInfo in proposedGroup.Plots)
                {
                    plotInfo.Plot.GroupId = group.Id;
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
                supervisorsForResponse = supervisorsList.ToDictionary(s => s.Id, s => s.FullName);
            }

            var response = new FormGroupsResponse
            {
                ClusterId = request.ClusterId,
                SeasonId = request.SeasonId,
                Year = request.Year,
                GroupsCreated = createdGroups.Count,
                PlotsGrouped = createdGroups.Sum(g => g.Plots.Count),
                UngroupedPlots = ungroupedPlots.Count,
                Groups = createdGroups.Select(g => new CreatedGroupDto
                {
                    GroupId = g.Id,
                    RiceVarietyId = g.RiceVarietyId!.Value,
                    RiceVarietyName = varietyDict.GetValueOrDefault(g.RiceVarietyId!.Value)?.VarietyName ?? "Unknown",
                    SupervisorId = g.SupervisorId,
                    SupervisorName = g.SupervisorId.HasValue ? supervisorsForResponse.GetValueOrDefault(g.SupervisorId.Value) : null,
                    PlantingDate = g.PlantingDate!.Value,
                    PlantingWindowStart = g.PlantingDate!.Value, // TODO: store actual window
                    PlantingWindowEnd = g.PlantingDate!.Value,
                    Status = g.Status.ToString(),
                    PlotCount = g.Plots.Count,
                    TotalArea = g.TotalArea!.Value,
                    GroupBoundaryWkt = g.Area != null ? _wktWriter.Write(g.Area) : null,
                    PlotIds = g.Plots.Select(p => p.Id).ToList()
                }).ToList(),
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
}

