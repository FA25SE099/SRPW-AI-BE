using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.ConfirmMaterialReceipt;

public class ConfirmMaterialReceiptCommandHandler 
    : IRequestHandler<ConfirmMaterialReceiptCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfirmMaterialReceiptCommandHandler> _logger;

    public ConfirmMaterialReceiptCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ConfirmMaterialReceiptCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ConfirmMaterialReceiptCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var distribution = await _unitOfWork.Repository<MaterialDistribution>()
                .GetQueryable()
                .Include(md => md.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .FirstOrDefaultAsync(md => md.Id == request.MaterialDistributionId, cancellationToken);

            if (distribution == null)
                return Result<bool>.Failure("Material distribution not found");

            if (distribution.Status == DistributionStatus.Completed)
                return Result<bool>.Failure("Material distribution already completed");

            if (distribution.Status == DistributionStatus.Rejected)
                return Result<bool>.Failure("Material distribution was rejected");

            if (distribution.Status != DistributionStatus.PartiallyConfirmed)
                return Result<bool>.Failure("Material must be confirmed by supervisor first");

            if (distribution.PlotCultivation.Plot.FarmerId != request.FarmerId)
                return Result<bool>.Failure("Farmer not authorized for this plot");

            distribution.FarmerConfirmedAt = DateTime.UtcNow;
            distribution.FarmerNotes = request.Notes;
            distribution.Status = DistributionStatus.Completed;

            _unitOfWork.Repository<MaterialDistribution>().Update(distribution);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Farmer {FarmerId} confirmed receipt of material distribution {DistributionId}",
                request.FarmerId, distribution.Id);

            return Result<bool>.Success(true, "Material receipt confirmed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming material receipt {DistributionId}", request.MaterialDistributionId);
            return Result<bool>.Failure($"Error confirming material receipt: {ex.Message}");
        }
    }
}

