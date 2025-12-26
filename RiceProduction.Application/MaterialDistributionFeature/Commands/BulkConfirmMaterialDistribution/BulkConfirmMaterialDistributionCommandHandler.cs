using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.BulkConfirmMaterialDistribution;

public class BulkConfirmMaterialDistributionCommandHandler 
    : IRequestHandler<BulkConfirmMaterialDistributionCommand, Result<BulkConfirmationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkConfirmMaterialDistributionCommandHandler> _logger;

    public BulkConfirmMaterialDistributionCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<BulkConfirmMaterialDistributionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<BulkConfirmationResponse>> Handle(
        BulkConfirmMaterialDistributionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all pending distributions for this plot cultivation
            var distributions = await _unitOfWork.Repository<MaterialDistribution>()
                .GetQueryable()
                .Include(md => md.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
                .Include(md => md.Material)
                .Where(md => 
                    md.PlotCultivationId == request.PlotCultivationId &&
                    md.Status == DistributionStatus.Pending)
                .ToListAsync(cancellationToken);

            if (!distributions.Any())
            {
                return Result<BulkConfirmationResponse>.Failure(
                    "No pending material distributions found for this plot cultivation");
            }

            // Verify supervisor authorization
            var group = distributions.First().PlotCultivation.Plot.GroupPlots.FirstOrDefault()?.Group;
            if (group != null && group.SupervisorId != request.SupervisorId)
            {
                return Result<BulkConfirmationResponse>.Failure(
                    "Supervisor not authorized for this group");
            }

            // Check if distribution is late
            var anyOverdue = distributions.Any(d => request.ActualDistributionDate > d.DistributionDeadline);
            if (anyOverdue)
            {
                _logger.LogWarning(
                    "Bulk distribution for PlotCultivation {PlotCultivationId} confirmed after deadline by Supervisor {SupervisorId}",
                    request.PlotCultivationId, request.SupervisorId);
            }

            // Get farmer confirmation window
            var farmerConfirmationWindow = await GetFarmerConfirmationWindowAsync(cancellationToken);
            var farmerDeadline = DateTime.UtcNow.AddDays(farmerConfirmationWindow);

            // Confirm all distributions
            var confirmedIds = new List<Guid>();
            foreach (var distribution in distributions)
            {
                distribution.SupervisorConfirmedBy = request.SupervisorId;
                distribution.SupervisorConfirmedAt = DateTime.UtcNow;
                distribution.ActualDistributionDate = request.ActualDistributionDate;
                distribution.SupervisorNotes = request.Notes;
                
                // Use individual distribution images if provided, otherwise fall back to shared images
                if (request.DistributionImages != null && 
                    request.DistributionImages.ContainsKey(distribution.Id))
                {
                    distribution.ImageUrls = request.DistributionImages[distribution.Id];
                    _logger.LogDebug(
                        "Using individual images for distribution {DistributionId}: {ImageCount} images",
                        distribution.Id, distribution.ImageUrls?.Count ?? 0);
                }
                else
                {
                    distribution.ImageUrls = request.ImageUrls; // Fallback to shared images
                    _logger.LogDebug(
                        "Using shared images for distribution {DistributionId}",
                        distribution.Id);
                }
                
                distribution.Status = DistributionStatus.PartiallyConfirmed;
                distribution.FarmerConfirmationDeadline = farmerDeadline;

                _unitOfWork.Repository<MaterialDistribution>().Update(distribution);
                confirmedIds.Add(distribution.Id);

                _logger.LogDebug(
                    "Confirmed distribution {DistributionId} for Material {MaterialName}",
                    distribution.Id, distribution.Material?.Name ?? "Unknown");
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Supervisor {SupervisorId} bulk confirmed {Count} material distributions for PlotCultivation {PlotCultivationId}",
                request.SupervisorId, distributions.Count, request.PlotCultivationId);

            var response = new BulkConfirmationResponse
            {
                TotalDistributionsConfirmed = distributions.Count,
                ConfirmedDistributionIds = confirmedIds,
                Message = $"Successfully confirmed {distributions.Count} material distributions"
            };

            return Result<BulkConfirmationResponse>.Success(
                response, 
                $"Bulk confirmation successful for {distributions.Count} materials");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error bulk confirming material distributions for PlotCultivation {PlotCultivationId}",
                request.PlotCultivationId);
            return Result<BulkConfirmationResponse>.Failure(
                $"Error confirming material distributions: {ex.Message}");
        }
    }

    private async Task<int> GetFarmerConfirmationWindowAsync(CancellationToken cancellationToken)
    {
        var setting = await _unitOfWork.Repository<SystemSetting>()
            .FindAsync(s => s.SettingKey == "FarmerConfirmationWindowDays");

        return setting != null && int.TryParse(setting.SettingValue, out var value) ? value : 3;
    }
}

