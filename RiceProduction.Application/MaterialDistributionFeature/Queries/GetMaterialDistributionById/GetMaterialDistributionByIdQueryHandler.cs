using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionById;

public class GetMaterialDistributionByIdQueryHandler 
    : IRequestHandler<GetMaterialDistributionByIdQuery, Result<MaterialDistributionDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMaterialDistributionByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MaterialDistributionDetailDto>> Handle(
        GetMaterialDistributionByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var distribution = await _unitOfWork.Repository<MaterialDistribution>()
            .GetQueryable()
            .Where(md => md.Id == request.DistributionId)
            .Include(md => md.Material)
            .Include(md => md.PlotCultivation)
                .ThenInclude(pc => pc.Plot)
            .Include(md => md.ConfirmedBy)
            .FirstOrDefaultAsync(cancellationToken);

        if (distribution == null)
        {
            return Result<MaterialDistributionDetailDto>.Failure("Material distribution not found");
        }

        var plot = distribution.PlotCultivation.Plot;
        
        // Get farmer info
        var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(plot.FarmerId);

        var now = DateTime.UtcNow;

        var isSupervisorOverdue = !distribution.SupervisorConfirmedAt.HasValue && 
                                 now > distribution.SupervisorConfirmationDeadline;
        var isFarmerOverdue = distribution.SupervisorConfirmedAt.HasValue && 
                             !distribution.FarmerConfirmedAt.HasValue && 
                             distribution.FarmerConfirmationDeadline.HasValue &&
                             now > distribution.FarmerConfirmationDeadline;
        var isDistributionOverdue = !distribution.ActualDistributionDate.HasValue &&
                                   now > distribution.DistributionDeadline;

        var dto = new MaterialDistributionDetailDto
        {
            Id = distribution.Id,
            PlotCultivationId = distribution.PlotCultivationId,
            PlotName =  "Unknown",
            FarmerId = plot.FarmerId,
            FarmerName = farmer?.FullName ?? "Unknown",
            FarmerPhone = farmer?.PhoneNumber,
            MaterialId = distribution.MaterialId,
            MaterialName = distribution.Material.Name,
            Quantity = distribution.QuantityDistributed,
            Unit = distribution.Material.Unit,
            Status = distribution.Status.ToString(),
            ScheduledDistributionDate = distribution.ScheduledDistributionDate,
            DistributionDeadline = distribution.DistributionDeadline,
            ActualDistributionDate = distribution.ActualDistributionDate,
            SupervisorConfirmationDeadline = distribution.SupervisorConfirmationDeadline,
            FarmerConfirmationDeadline = distribution.FarmerConfirmationDeadline,
            SupervisorConfirmedBy = distribution.SupervisorConfirmedBy,
            SupervisorName = distribution.ConfirmedBy?.FullName,
            SupervisorConfirmedAt = distribution.SupervisorConfirmedAt,
            SupervisorNotes = distribution.SupervisorNotes,
            FarmerConfirmedAt = distribution.FarmerConfirmedAt,
            FarmerNotes = distribution.FarmerNotes,
            ImageUrls = distribution.ImageUrls,
            IsOverdue = isSupervisorOverdue || isFarmerOverdue || isDistributionOverdue,
            IsSupervisorOverdue = isSupervisorOverdue,
            IsFarmerOverdue = isFarmerOverdue
        };

        return Result<MaterialDistributionDetailDto>.Success(dto);
    }
}

