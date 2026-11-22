using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

public class GetMaterialDistributionsForGroupQueryHandler
    : IRequestHandler<GetMaterialDistributionsForGroupQuery, Result<MaterialDistributionsResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetMaterialDistributionsForGroupQueryHandler> _logger;

    public GetMaterialDistributionsForGroupQueryHandler(
        IApplicationDbContext context,
        ILogger<GetMaterialDistributionsForGroupQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<MaterialDistributionsResponse>> Handle(
        GetMaterialDistributionsForGroupQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var groupExists = await _context.Groups
                .AsNoTracking()
                .AnyAsync(g => g.Id == request.GroupId, cancellationToken);

            if (!groupExists)
                return Result<MaterialDistributionsResponse>.Failure("Group not found");

            var plotIds = await _context.GroupPlots
                .AsNoTracking()
                .Where(gp => gp.GroupId == request.GroupId)
                .Select(gp => gp.PlotId)
                .ToListAsync(cancellationToken);

            var plotCultivations = await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotIds.Contains(pc.PlotId))
                .Select(pc => new { pc.Id, pc.Plot.FarmerId })
                .ToListAsync(cancellationToken);

            var cultivationIds = plotCultivations.Select(pc => pc.Id).ToList();

            var distributions = await _context.MaterialDistributions
                .AsNoTracking()
                .Where(md => cultivationIds.Contains(md.PlotCultivationId))
                .Include(md => md.Material)
                .Include(md => md.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(md => md.ConfirmedBy)
                .ToListAsync(cancellationToken);

            var farmerIds = plotCultivations.Select(pc => pc.FarmerId).Distinct().ToList();
            var farmerDict = await _context.Farmers
                .AsNoTracking()
                .Where(f => farmerIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, cancellationToken);

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

