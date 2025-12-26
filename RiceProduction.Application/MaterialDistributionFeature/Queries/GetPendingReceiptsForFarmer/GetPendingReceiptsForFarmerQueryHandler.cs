using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetPendingReceiptsForFarmer;

public class GetPendingReceiptsForFarmerQueryHandler 
    : IRequestHandler<GetPendingReceiptsForFarmerQuery, Result<PendingReceiptsForFarmerResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPendingReceiptsForFarmerQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PendingReceiptsForFarmerResponse>> Handle(
        GetPendingReceiptsForFarmerQuery request, 
        CancellationToken cancellationToken)
    {
        // Verify farmer exists
        var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.FarmerId);
        if (farmer == null)
        {
            return Result<PendingReceiptsForFarmerResponse>.Failure("Farmer not found");
        }

        // Get all plots owned by this farmer
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => p.FarmerId == request.FarmerId);

        if (!plots.Any())
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

        var plotIds = plots.Select(p => p.Id).ToList();

        // Get all plot cultivations for these plots
        var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
            .GetQueryable()
            .Where(pc => plotIds.Contains(pc.PlotId))
            .Include(pc => pc.Plot)
            .ToListAsync(cancellationToken);

        var cultivationIds = plotCultivations.Select(pc => pc.Id).ToList();

        // Get distributions where supervisor confirmed but farmer hasn't (PartiallyConfirmed status)
        var pendingReceipts = await _unitOfWork.Repository<MaterialDistribution>()
            .GetQueryable()
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

