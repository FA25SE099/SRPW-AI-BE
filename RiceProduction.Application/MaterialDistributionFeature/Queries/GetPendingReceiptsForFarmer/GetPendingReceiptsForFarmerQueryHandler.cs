using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetPendingReceiptsForFarmer;

public class GetPendingReceiptsForFarmerQueryHandler
    : IRequestHandler<GetPendingReceiptsForFarmerQuery, Result<PendingReceiptsForFarmerResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingReceiptsForFarmerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PendingReceiptsForFarmerResponse>> Handle(
        GetPendingReceiptsForFarmerQuery request,
        CancellationToken cancellationToken)
    {
        var farmer = await _context.Farmers
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FarmerId, cancellationToken);
        if (farmer == null)
        {
            return Result<PendingReceiptsForFarmerResponse>.Failure("Farmer not found");
        }

        var plotIds = await _context.Plots
            .AsNoTracking()
            .Where(p => p.FarmerId == request.FarmerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (plotIds.Count == 0)
        {
            return Result<PendingReceiptsForFarmerResponse>.Success(
                new PendingReceiptsForFarmerResponse
                {
                    FarmerId = request.FarmerId,
                    TotalPending = 0,
                    OverdueCount = 0,
                    DueTodayCount = 0,
                    DueTomorrowCount = 0,
                    PendingReceipts = new List<MaterialDistributionDetailDto>()
                });
        }

        var cultivationIds = await _context.PlotCultivations
            .AsNoTracking()
            .Where(pc => plotIds.Contains(pc.PlotId))
            .Select(pc => pc.Id)
            .ToListAsync(cancellationToken);

        // Get distributions where supervisor confirmed but farmer hasn't (PartiallyConfirmed status)
        var pendingReceipts = await _context.MaterialDistributions
            .AsNoTracking()
            .Where(md => cultivationIds.Contains(md.PlotCultivationId) &&
                        md.Status == DistributionStatus.PartiallyConfirmed &&
                        md.SupervisorConfirmedAt.HasValue &&
                        !md.FarmerConfirmedAt.HasValue)
            .Include(md => md.Material)
            .Include(md => md.PlotCultivation)
                .ThenInclude(pc => pc.Plot)
            .Include(md => md.ConfirmedBy)
            .OrderBy(md => md.FarmerConfirmationDeadline)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        // Map to DTOs
        var receiptDtos = pendingReceipts.Select(d =>
        {
            var plot = d.PlotCultivation.Plot;

            var isFarmerOverdue = d.SupervisorConfirmedAt.HasValue && 
                                 !d.FarmerConfirmedAt.HasValue && 
                                 d.FarmerConfirmationDeadline.HasValue &&
                                 now > d.FarmerConfirmationDeadline;

            return new MaterialDistributionDetailDto
            {
                Id = d.Id,
                PlotCultivationId = d.PlotCultivationId,
                PlotName =  "Unknown",
                FarmerId = plot.FarmerId,
                FarmerName = farmer.FullName,
                FarmerPhone = farmer.PhoneNumber,
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
                IsOverdue = isFarmerOverdue,
                IsSupervisorOverdue = false, // Already confirmed by supervisor
                IsFarmerOverdue = isFarmerOverdue
            };
        }).ToList();

        // Calculate counts
        var overdueCount = receiptDtos.Count(d => d.IsFarmerOverdue);
        var dueTodayCount = receiptDtos.Count(d => 
            !d.IsFarmerOverdue && 
            d.FarmerConfirmationDeadline.HasValue &&
            d.FarmerConfirmationDeadline.Value.Date == today);
        var dueTomorrowCount = receiptDtos.Count(d => 
            !d.IsFarmerOverdue && 
            d.FarmerConfirmationDeadline.HasValue &&
            d.FarmerConfirmationDeadline.Value.Date == tomorrow);

        var response = new PendingReceiptsForFarmerResponse
        {
            FarmerId = request.FarmerId,
            TotalPending = receiptDtos.Count,
            OverdueCount = overdueCount,
            DueTodayCount = dueTodayCount,
            DueTomorrowCount = dueTomorrowCount,
            PendingReceipts = receiptDtos
        };

        return Result<PendingReceiptsForFarmerResponse>.Success(response);
    }
}

