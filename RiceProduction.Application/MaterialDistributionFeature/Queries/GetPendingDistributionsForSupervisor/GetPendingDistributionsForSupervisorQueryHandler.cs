//using Microsoft.EntityFrameworkCore;
//using RiceProduction.Application.Common.Interfaces;
//using RiceProduction.Application.Common.Models;
//using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;
//using RiceProduction.Domain.Entities;
//using RiceProduction.Domain.Enums;

//namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetPendingDistributionsForSupervisor;

//public class GetPendingDistributionsForSupervisorQueryHandler 
//    : IRequestHandler<GetPendingDistributionsForSupervisorQuery, Result<PendingDistributionsForSupervisorResponse>>
//{
//    private readonly IUnitOfWork _unitOfWork;

//    public GetPendingDistributionsForSupervisorQueryHandler(IUnitOfWork unitOfWork)
//    {
//        _unitOfWork = unitOfWork;
//    }

//    public async Task<Result<PendingDistributionsForSupervisorResponse>> Handle(
//        GetPendingDistributionsForSupervisorQuery request, 
//        CancellationToken cancellationToken)
//    {
//        // Verify supervisor exists
//        var supervisor = await _unitOfWork.SupervisorRepository.FindAsync(c => c.Id ==request.SupervisorId);
//        if (supervisor == null)
//        {
//            return Result<PendingDistributionsForSupervisorResponse>.Failure("Supervisor not found");
//        }

//        // Get all groups supervised by this supervisor
//        var groups = await _unitOfWork.Repository<Group>()
//            .ListAsync(g => g.SupervisorId == request.SupervisorId);

//        if (!groups.Any())
//        {
//            return Result<PendingDistributionsForSupervisorResponse>.Success(
//                new PendingDistributionsForSupervisorResponse
//                {
//                    SupervisorId = request.SupervisorId,
//                    TotalPending = 0,
//                    OverdueCount = 0,
//                    DueTodayCount = 0,
//                    DueTomorrowCount = 0,
//                    PendingDistributions = new List<MaterialDistributionDetailDto>()
//                });
//        }

//        var groupIds = groups.Select(g => g.Id).ToList();

//        // Get all plot cultivations for these groups
//        var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
//            .GetQueryable()
//            .Where(pc => groupIds.Contains(pc.GroupId))
//            .Include(pc => pc.Plot)
//            .ToListAsync(cancellationToken);

//        var cultivationIds = plotCultivations.Select(pc => pc.Id).ToList();

//        // Get all pending distributions (not yet confirmed by supervisor)
//        var pendingDistributions = await _unitOfWork.Repository<MaterialDistribution>()
//            .GetQueryable()
//            .Where(md => cultivationIds.Contains(md.PlotCultivationId) && 
//                        md.Status == DistributionStatus.Pending)
//            .Include(md => md.Material)
//            .Include(md => md.PlotCultivation)
//                .ThenInclude(pc => pc.Plot)
//            .Include(md => md.ConfirmedBy)
//            .OrderBy(md => md.SupervisorConfirmationDeadline)
//            .ToListAsync(cancellationToken);

//        // Get farmers
//        var farmerIds = plotCultivations.Select(pc => pc.Plot.FarmerId).Distinct().ToList();
//        var farmers = await _unitOfWork.FarmerRepository
//            .ListAsync(f => farmerIds.Contains(f.Id));
//        var farmerDict = farmers.ToDictionary(f => f.Id);

//        var now = DateTime.UtcNow;
//        var today = now.Date;
//        var tomorrow = today.AddDays(1);

//        // Map to DTOs
//        var distributionDtos = pendingDistributions.Select(d =>
//        {
//            var plot = d.PlotCultivation.Plot;
//            var farmer = farmerDict.GetValueOrDefault(plot.FarmerId);

//            var isSupervisorOverdue = !d.SupervisorConfirmedAt.HasValue && 
//                                     now > d.SupervisorConfirmationDeadline;
//            var isDistributionOverdue = !d.ActualDistributionDate.HasValue &&
//                                       now > d.DistributionDeadline;

//            return new MaterialDistributionDetailDto
//            {
//                Id = d.Id,
//                PlotCultivationId = d.PlotCultivationId,
//                PlotName = plot.PlotName ?? "Unknown",
//                FarmerId = plot.FarmerId,
//                FarmerName = farmer?.FullName ?? "Unknown",
//                FarmerPhone = farmer?.PhoneNumber,
//                MaterialId = d.MaterialId,
//                MaterialName = d.Material.Name,
//                Quantity = d.QuantityDistributed,
//                Unit = d.Material.Unit,
//                Status = d.Status.ToString(),
//                ScheduledDistributionDate = d.ScheduledDistributionDate,
//                DistributionDeadline = d.DistributionDeadline,
//                ActualDistributionDate = d.ActualDistributionDate,
//                SupervisorConfirmationDeadline = d.SupervisorConfirmationDeadline,
//                FarmerConfirmationDeadline = d.FarmerConfirmationDeadline,
//                SupervisorConfirmedBy = d.SupervisorConfirmedBy,
//                SupervisorName = d.ConfirmedBy?.FullName,
//                SupervisorConfirmedAt = d.SupervisorConfirmedAt,
//                SupervisorNotes = d.SupervisorNotes,
//                FarmerConfirmedAt = d.FarmerConfirmedAt,
//                FarmerNotes = d.FarmerNotes,
//                ImageUrls = d.ImageUrls,
//                IsOverdue = isSupervisorOverdue || isDistributionOverdue,
//                IsSupervisorOverdue = isSupervisorOverdue,
//                IsFarmerOverdue = false // Not applicable for pending distributions
//            };
//        }).ToList();

//        // Calculate counts
//        var overdueCount = distributionDtos.Count(d => d.IsSupervisorOverdue);
//        var dueTodayCount = distributionDtos.Count(d => 
//            !d.IsSupervisorOverdue && 
//            d.SupervisorConfirmationDeadline.Date == today);
//        var dueTomorrowCount = distributionDtos.Count(d => 
//            !d.IsSupervisorOverdue && 
//            d.SupervisorConfirmationDeadline.Date == tomorrow);

//        var response = new PendingDistributionsForSupervisorResponse
//        {
//            SupervisorId = request.SupervisorId,
//            TotalPending = distributionDtos.Count,
//            OverdueCount = overdueCount,
//            DueTodayCount = dueTodayCount,
//            DueTomorrowCount = dueTomorrowCount,
//            PendingDistributions = distributionDtos
//        };

//        return Result<PendingDistributionsForSupervisorResponse>.Success(response);
//    }
//}

