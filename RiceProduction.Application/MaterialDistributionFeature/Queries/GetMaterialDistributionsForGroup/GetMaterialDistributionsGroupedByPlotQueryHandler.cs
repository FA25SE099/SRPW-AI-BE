using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

public class GetMaterialDistributionsGroupedByPlotQueryHandler 
    : IRequestHandler<GetMaterialDistributionsGroupedByPlotQuery, Result<GroupedMaterialDistributionsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetMaterialDistributionsGroupedByPlotQueryHandler> _logger;

    public GetMaterialDistributionsGroupedByPlotQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetMaterialDistributionsGroupedByPlotQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<GroupedMaterialDistributionsResponse>> Handle(
        GetMaterialDistributionsGroupedByPlotQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = await _unitOfWork.Repository<Group>()
                .FindAsync(g => g.Id == request.GroupId);

            if (group == null)
                return Result<GroupedMaterialDistributionsResponse>.Failure("Group not found");

            // Get all plots in the group
            var groupPlots = await _unitOfWork.Repository<GroupPlot>()
                .ListAsync(gp => gp.GroupId == request.GroupId);
            var plotIds = groupPlots.Select(gp => gp.PlotId).ToList();

            // Get plot cultivations
            var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .Where(pc => plotIds.Contains(pc.PlotId))
                .Include(pc => pc.Plot)
                .ToListAsync(cancellationToken);

            var cultivationIds = plotCultivations.Select(pc => pc.Id).ToList();

            // Get all distributions
            var distributions = await _unitOfWork.Repository<MaterialDistribution>()
                .GetQueryable()
                .Where(md => cultivationIds.Contains(md.PlotCultivationId))
                .Include(md => md.Material)
                .Include(md => md.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(md => md.ConfirmedBy)
                .ToListAsync(cancellationToken);

            // Get farmers
            var farmerIds = plotCultivations.Select(pc => pc.Plot.FarmerId).Distinct().ToList();
            var farmers = await _unitOfWork.FarmerRepository
                .ListAsync(f => farmerIds.Contains(f.Id));
            var farmerDict = farmers.ToDictionary(f => f.Id);

            var now = DateTime.UtcNow;

            // Group distributions by plot cultivation (farmer)
            var groupedByPlot = distributions
                .GroupBy(d => d.PlotCultivationId)
                .Select(g =>
                {
                    var firstDist = g.First();
                    var plot = firstDist.PlotCultivation.Plot;
                    var farmer = farmerDict.GetValueOrDefault(plot.FarmerId);

                    // Determine overall status
                    var allConfirmedBySupervisor = g.All(d => d.SupervisorConfirmedAt.HasValue);
                    var allConfirmedByFarmer = g.All(d => d.FarmerConfirmedAt.HasValue);
                    var anyPending = g.Any(d => d.Status == DistributionStatus.Pending);

                    string overallStatus;
                    if (allConfirmedByFarmer)
                        overallStatus = "Completed";
                    else if (allConfirmedBySupervisor)
                        overallStatus = "PartiallyConfirmed";
                    else if (anyPending)
                        overallStatus = "Pending";
                    else
                        overallStatus = "Unknown";

                    // Check overdue flags
                    var isSupervisorOverdue = !allConfirmedBySupervisor && 
                                             now > firstDist.SupervisorConfirmationDeadline;
                    var isFarmerOverdue = allConfirmedBySupervisor && 
                                         !allConfirmedByFarmer && 
                                         firstDist.FarmerConfirmationDeadline.HasValue &&
                                         now > firstDist.FarmerConfirmationDeadline;
                    var isDistributionOverdue = !firstDist.ActualDistributionDate.HasValue &&
                                               now > firstDist.DistributionDeadline;

                    return new FarmerMaterialDistribution
                    {
                        PlotCultivationId = g.Key,
                        PlotId = plot.Id,
                        PlotName =  "Unknown",
                        FarmerId = plot.FarmerId,
                        FarmerName = farmer?.FullName ?? "Unknown",
                        FarmerPhone = farmer?.PhoneNumber,
                        Location = "Unknown", // Add location if available in Plot entity
                        Status = overallStatus,
                        
                        // Materials list
                        Materials = g.Select(d => new MaterialItem
                        {
                            DistributionId = d.Id,
                            MaterialId = d.MaterialId,
                            MaterialName = d.Material?.Name ?? "Unknown",
                            Quantity = d.QuantityDistributed,
                            Unit = d.Material?.Unit ?? "",
                            Status = d.Status.ToString(),
                            FarmerConfirmedAt = d.FarmerConfirmedAt,
                            FarmerNotes = d.FarmerNotes
                        }).ToList(),
                        
                        // Scheduling (same for all materials in bulk)
                        ScheduledDistributionDate = firstDist.ScheduledDistributionDate,
                        DistributionDeadline = firstDist.DistributionDeadline,
                        SupervisorConfirmationDeadline = firstDist.SupervisorConfirmationDeadline,
                        FarmerConfirmationDeadline = firstDist.FarmerConfirmationDeadline,
                        
                        // Confirmation info (same for all since bulk confirmed)
                        SupervisorConfirmedBy = firstDist.SupervisorConfirmedBy,
                        SupervisorName = firstDist.ConfirmedBy?.FullName,
                        SupervisorConfirmedAt = firstDist.SupervisorConfirmedAt,
                        ActualDistributionDate = firstDist.ActualDistributionDate,
                        SupervisorNotes = firstDist.SupervisorNotes,
                        ImageUrls = firstDist.ImageUrls,
                        
                        // Flags
                        IsOverdue = isDistributionOverdue,
                        IsSupervisorOverdue = isSupervisorOverdue,
                        IsFarmerOverdue = isFarmerOverdue,
                        
                        // Counts
                        TotalMaterialCount = g.Count(),
                        PendingMaterialCount = g.Count(d => d.Status == DistributionStatus.Pending),
                        ConfirmedMaterialCount = g.Count(d => d.Status == DistributionStatus.Completed)
                    };
                })
                .OrderBy(f => f.FarmerName)
                .ToList();

            var response = new GroupedMaterialDistributionsResponse
            {
                GroupId = request.GroupId,
                TotalFarmers = groupedByPlot.Count,
                TotalMaterials = distributions.Count,
                FarmerDistributions = groupedByPlot
            };

            return Result<GroupedMaterialDistributionsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving grouped material distributions for group {GroupId}", request.GroupId);
            return Result<GroupedMaterialDistributionsResponse>.Failure($"Error retrieving material distributions: {ex.Message}");
        }
    }
}

