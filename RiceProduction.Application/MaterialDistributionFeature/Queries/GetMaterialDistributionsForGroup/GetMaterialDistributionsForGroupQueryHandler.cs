using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

public class GetMaterialDistributionsForGroupQueryHandler 
    : IRequestHandler<GetMaterialDistributionsForGroupQuery, Result<MaterialDistributionsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetMaterialDistributionsForGroupQueryHandler> _logger;

    public GetMaterialDistributionsForGroupQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetMaterialDistributionsForGroupQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<MaterialDistributionsResponse>> Handle(
        GetMaterialDistributionsForGroupQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = await _unitOfWork.Repository<Group>()
                .FindAsync(g => g.Id == request.GroupId);

            if (group == null)
                return Result<MaterialDistributionsResponse>.Failure("Group not found");

            var groupPlots = await _unitOfWork.Repository<GroupPlot>()
                .ListAsync(gp => gp.GroupId == request.GroupId);
            var plotIds = groupPlots.Select(gp => gp.PlotId).ToList();

            var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .Where(pc => plotIds.Contains(pc.PlotId))
                .Include(pc => pc.Plot)
                .ToListAsync(cancellationToken);

            var cultivationIds = plotCultivations.Select(pc => pc.Id).ToList();

            var distributions = await _unitOfWork.Repository<MaterialDistribution>()
                .GetQueryable()
                .Where(md => cultivationIds.Contains(md.PlotCultivationId))
                .Include(md => md.Material)
                .Include(md => md.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(md => md.ConfirmedBy)
                .ToListAsync(cancellationToken);

            var farmerIds = plotCultivations.Select(pc => pc.Plot.FarmerId).Distinct().ToList();
            var farmers = await _unitOfWork.FarmerRepository
                .ListAsync(f => farmerIds.Contains(f.Id));
            var farmerDict = farmers.ToDictionary(f => f.Id);

            var now = DateTime.UtcNow;

            var response = new MaterialDistributionsResponse
            {
                GroupId = request.GroupId,
                TotalDistributions = distributions.Count,
                PendingCount = distributions.Count(d => d.Status == DistributionStatus.Pending),
                PartiallyConfirmedCount = distributions.Count(d => d.Status == DistributionStatus.PartiallyConfirmed),
                CompletedCount = distributions.Count(d => d.Status == DistributionStatus.Completed),
                RejectedCount = distributions.Count(d => d.Status == DistributionStatus.Rejected),
                Distributions = distributions.Select(d =>
                {
                    var plot = d.PlotCultivation.Plot;
                    var farmer = farmerDict.GetValueOrDefault(plot.FarmerId);

                    var isSupervisorOverdue = !d.SupervisorConfirmedAt.HasValue && 
                                             now > d.SupervisorConfirmationDeadline;
                    var isFarmerOverdue = d.SupervisorConfirmedAt.HasValue && 
                                         !d.FarmerConfirmedAt.HasValue && 
                                         d.FarmerConfirmationDeadline.HasValue &&
                                         now > d.FarmerConfirmationDeadline;
                    var isDistributionOverdue = !d.ActualDistributionDate.HasValue &&
                                               now > d.DistributionDeadline;

                    return new MaterialDistributionDetailDto
                    {
                        Id = d.Id,
                        PlotCultivationId = d.PlotCultivationId,
                        PlotName =  "Unknown",
                        FarmerId = plot.FarmerId,
                        FarmerName = farmer?.FullName ?? "Unknown",
                        FarmerPhone = farmer?.PhoneNumber,
                        MaterialId = d.MaterialId,
                        MaterialName = d.Material.Name,
                        Quantity = d.QuantityDistributed,
                        Unit = d.Material.Unit,
                        Status = d.Status.ToString(),
                        ScheduledDistributionDate = d.ScheduledDistributionDate,
                        DistributionDeadline = d.DistributionDeadline,
                        ActualDistributionDate = d.ActualDistributionDate,
                        SupervisorConfirmationDeadline = d.SupervisorConfirmationDeadline,
                        FarmerConfirmationDeadline = d.FarmerConfirmationDeadline,
                        SupervisorConfirmedBy = d.SupervisorConfirmedBy,
                        SupervisorName = d.ConfirmedBy?.FullName,
                        SupervisorConfirmedAt = d.SupervisorConfirmedAt,
                        SupervisorNotes = d.SupervisorNotes,
                        FarmerConfirmedAt = d.FarmerConfirmedAt,
                        FarmerNotes = d.FarmerNotes,
                        ImageUrls = d.ImageUrls,
                        IsOverdue = isDistributionOverdue,
                        IsSupervisorOverdue = isSupervisorOverdue,
                        IsFarmerOverdue = isFarmerOverdue
                    };
                }).ToList()
            };

            return Result<MaterialDistributionsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving material distributions for group {GroupId}", request.GroupId);
            return Result<MaterialDistributionsResponse>.Failure($"Error retrieving material distributions: {ex.Message}");
        }
    }
}

