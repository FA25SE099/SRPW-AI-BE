using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.BulkConfirmMaterialReceipt;

public class BulkConfirmMaterialReceiptCommandHandler 
    : IRequestHandler<BulkConfirmMaterialReceiptCommand, Result<BulkReceiptConfirmationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkConfirmMaterialReceiptCommandHandler> _logger;

    public BulkConfirmMaterialReceiptCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<BulkConfirmMaterialReceiptCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<BulkReceiptConfirmationResponse>> Handle(
        BulkConfirmMaterialReceiptCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.DistributionIds == null || !request.DistributionIds.Any())
            {
                return Result<BulkReceiptConfirmationResponse>.Failure(
                    "No distribution IDs provided");
            }

            // Verify farmer exists
            var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.FarmerId);
            if (farmer == null)
            {
                return Result<BulkReceiptConfirmationResponse>.Failure("Farmer not found");
            }

            // Get all distributions that match the provided IDs
            var distributions = await _unitOfWork.Repository<MaterialDistribution>()
                .GetQueryable()
                .Include(md => md.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(md => md.Material)
                .Where(md => request.DistributionIds.Contains(md.Id))
                .ToListAsync(cancellationToken);

            if (!distributions.Any())
            {
                return Result<BulkReceiptConfirmationResponse>.Failure(
                    "No matching distributions found");
            }

            var confirmedIds = new List<Guid>();
            var failedReasons = new List<string>();
            var now = DateTime.UtcNow;

            // Process each distribution
            foreach (var distribution in distributions)
            {
                // Validate each distribution
                if (distribution.PlotCultivation.Plot.FarmerId != request.FarmerId)
                {
                    failedReasons.Add($"Distribution {distribution.Id}: Farmer not authorized for this plot");
                    _logger.LogWarning(
                        "Farmer {FarmerId} attempted to confirm unauthorized distribution {DistributionId}",
                        request.FarmerId, distribution.Id);
                    continue;
                }

                if (distribution.Status == DistributionStatus.Completed)
                {
                    failedReasons.Add($"Distribution {distribution.Id}: Already completed");
                    continue;
                }

                if (distribution.Status == DistributionStatus.Rejected)
                {
                    failedReasons.Add($"Distribution {distribution.Id}: Was rejected");
                    continue;
                }

                if (distribution.Status != DistributionStatus.PartiallyConfirmed)
                {
                    failedReasons.Add($"Distribution {distribution.Id}: Must be confirmed by supervisor first");
                    continue;
                }

                // Check if farmer is confirming after deadline
                if (distribution.FarmerConfirmationDeadline.HasValue && 
                    now > distribution.FarmerConfirmationDeadline.Value)
                {
                    _logger.LogWarning(
                        "Farmer {FarmerId} confirmed distribution {DistributionId} after deadline",
                        request.FarmerId, distribution.Id);
                }

                // Confirm the distribution
                distribution.FarmerConfirmedAt = now;
                distribution.FarmerNotes = request.Notes;
                distribution.Status = DistributionStatus.Completed;

                _unitOfWork.Repository<MaterialDistribution>().Update(distribution);
                confirmedIds.Add(distribution.Id);

                _logger.LogDebug(
                    "Confirmed receipt for distribution {DistributionId} - Material: {MaterialName}",
                    distribution.Id, distribution.Material?.Name ?? "Unknown");
            }

            // Save all changes
            if (confirmedIds.Any())
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Farmer {FarmerId} bulk confirmed {Count} material receipts ({Failed} failed)",
                    request.FarmerId, confirmedIds.Count, failedReasons.Count);
            }

            var response = new BulkReceiptConfirmationResponse
            {
                TotalReceiptsConfirmed = confirmedIds.Count,
                ConfirmedDistributionIds = confirmedIds,
                FailedCount = failedReasons.Count,
                FailedReasons = failedReasons,
                Message = confirmedIds.Any() 
                    ? $"Successfully confirmed {confirmedIds.Count} material receipt(s)" 
                    : "No receipts were confirmed"
            };

            if (!confirmedIds.Any())
            {
                return Result<BulkReceiptConfirmationResponse>.Failure(
                    response.Message,
                    "Failed to confirm any receipts. See FailedReasons for details.");
            }

            var message = failedReasons.Any()
                ? $"Confirmed {confirmedIds.Count} receipt(s), {failedReasons.Count} failed"
                : $"Successfully confirmed all {confirmedIds.Count} receipt(s)";

            return Result<BulkReceiptConfirmationResponse>.Success(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error bulk confirming material receipts for Farmer {FarmerId}",
                request.FarmerId);
            return Result<BulkReceiptConfirmationResponse>.Failure(
                $"Error confirming material receipts: {ex.Message}");
        }
    }
}

