using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.ConfirmMaterialDistribution;

public class ConfirmMaterialDistributionCommandHandler 
    : IRequestHandler<ConfirmMaterialDistributionCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfirmMaterialDistributionCommandHandler> _logger;

    public ConfirmMaterialDistributionCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ConfirmMaterialDistributionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ConfirmMaterialDistributionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var distribution = await _unitOfWork.Repository<MaterialDistribution>()
                .GetQueryable()
                .Include(md => md.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
                .FirstOrDefaultAsync(md => md.Id == request.MaterialDistributionId, cancellationToken);

            if (distribution == null)
                return Result<bool>.Failure("Material distribution not found");

            if (distribution.Status == DistributionStatus.Completed)
                return Result<bool>.Failure("Material distribution already completed");

            if (distribution.Status == DistributionStatus.Rejected)
                return Result<bool>.Failure("Material distribution was rejected");

            var group = distribution.PlotCultivation.Plot.GroupPlots.FirstOrDefault()?.Group;
            if (group != null && group.SupervisorId != request.SupervisorId)
                return Result<bool>.Failure("Supervisor not authorized for this group");

            if (request.ActualDistributionDate > distribution.DistributionDeadline)
            {
                _logger.LogWarning(
                    "Material distribution {DistributionId} confirmed after deadline. Deadline: {Deadline}, Actual: {Actual}",
                    distribution.Id, distribution.DistributionDeadline, request.ActualDistributionDate);
            }

            var farmerConfirmationWindow = await GetFarmerConfirmationWindow();

            distribution.SupervisorConfirmedBy = request.SupervisorId;
            distribution.SupervisorConfirmedAt = DateTime.UtcNow;
            distribution.ActualDistributionDate = request.ActualDistributionDate;
            distribution.SupervisorNotes = request.Notes;
            distribution.ImageUrls = request.ImageUrls;
            distribution.Status = DistributionStatus.PartiallyConfirmed;
            distribution.FarmerConfirmationDeadline = DateTime.UtcNow.AddDays(farmerConfirmationWindow);

            _unitOfWork.Repository<MaterialDistribution>().Update(distribution);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Supervisor {SupervisorId} confirmed material distribution {DistributionId}",
                request.SupervisorId, distribution.Id);

            return Result<bool>.Success(true, "Material distribution confirmed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming material distribution {DistributionId}", request.MaterialDistributionId);
            return Result<bool>.Failure($"Error confirming material distribution: {ex.Message}");
        }
    }

    private async Task<int> GetFarmerConfirmationWindow()
    {
        var setting = await _unitOfWork.Repository<SystemSetting>()
            .FindAsync(s => s.SettingKey == "FarmerConfirmationWindowDays");

        return setting != null && int.TryParse(setting.SettingValue, out var value) ? value : 3;
    }
}

