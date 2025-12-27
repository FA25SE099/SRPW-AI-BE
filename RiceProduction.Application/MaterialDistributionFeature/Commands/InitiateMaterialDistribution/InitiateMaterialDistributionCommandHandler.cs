using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.InitiateMaterialDistribution;

public class InitiateMaterialDistributionCommandHandler 
    : IRequestHandler<InitiateMaterialDistributionCommand, Result<InitiateMaterialDistributionResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InitiateMaterialDistributionCommandHandler> _logger;

    public InitiateMaterialDistributionCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<InitiateMaterialDistributionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<InitiateMaterialDistributionResponse>> Handle(
        InitiateMaterialDistributionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = await _unitOfWork.Repository<Group>()
                .FindAsync(g => g.Id == request.GroupId);

            if (group == null)
                return Result<InitiateMaterialDistributionResponse>.Failure("Group not found");

            if (group.Status != GroupStatus.Active)
                return Result<InitiateMaterialDistributionResponse>.Failure("Group must be in Active status");

            var productionPlan = await _unitOfWork.Repository<ProductionPlan>()
                .FindAsync(pp => pp.Id == request.ProductionPlanId && pp.GroupId == request.GroupId);

            if (productionPlan == null)
                return Result<InitiateMaterialDistributionResponse>.Failure("Production plan not found");

            if (productionPlan.Status != TaskStatus.Approved)
                return Result<InitiateMaterialDistributionResponse>.Failure("Production plan must be approved");

            var settings = await GetDistributionSettings();

            var distributions = new List<MaterialDistribution>();
            var plotCultivationIds = request.Materials.Select(m => m.PlotCultivationId).Distinct().ToList();
            
            var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
                .ListAsync(pc => plotCultivationIds.Contains(pc.Id));
            var plotCultivationDict = plotCultivations.ToDictionary(pc => pc.Id);

            var plotIds = plotCultivations.Select(pc => pc.PlotId).ToList();
            var plots = await _unitOfWork.Repository<Plot>()
                .ListAsync(p => plotIds.Contains(p.Id));
            var plotDict = plots.ToDictionary(p => p.Id);

            var farmerIds = plots.Select(p => p.FarmerId).Distinct().ToList();
            var farmers = await _unitOfWork.FarmerRepository
                .ListAsync(f => farmerIds.Contains(f.Id));
            var farmerDict = farmers.ToDictionary(f => f.Id);

            var materialIds = request.Materials.Select(m => m.MaterialId).Distinct().ToList();
            var materials = await _unitOfWork.Repository<Material>()
                .ListAsync(m => materialIds.Contains(m.Id));
            var materialDict = materials.ToDictionary(m => m.Id);

            foreach (var item in request.Materials)
            {
                if (!plotCultivationDict.ContainsKey(item.PlotCultivationId))
                    return Result<InitiateMaterialDistributionResponse>.Failure($"PlotCultivation {item.PlotCultivationId} not found");

                if (!materialDict.ContainsKey(item.MaterialId))
                    return Result<InitiateMaterialDistributionResponse>.Failure($"Material {item.MaterialId} not found");

                var existingDistribution = await _unitOfWork.Repository<MaterialDistribution>()
                    .FindAsync(md => md.PlotCultivationId == item.PlotCultivationId && 
                                    md.MaterialId == item.MaterialId &&
                                    md.Status != DistributionStatus.Rejected);

                if (existingDistribution != null)
                    continue;

                var scheduledDate = item.ScheduledDate;
                var distributionDeadline = scheduledDate.AddDays(-1);
                var supervisorDeadline = scheduledDate.AddDays(settings.SupervisorConfirmationWindow);

                var distribution = new MaterialDistribution
                {
                    PlotCultivationId = item.PlotCultivationId,
                    MaterialId = item.MaterialId,
                    RelatedTaskId = item.RelatedTaskId,
                    QuantityDistributed = item.Quantity,
                    Status = DistributionStatus.Pending,
                    ScheduledDistributionDate = scheduledDate,
                    DistributionDeadline = distributionDeadline,
                    SupervisorConfirmationDeadline = supervisorDeadline
                };

                await _unitOfWork.Repository<MaterialDistribution>().AddAsync(distribution);
                distributions.Add(distribution);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Initiated {Count} material distributions for group {GroupId}",
                distributions.Count, request.GroupId);

            var response = new InitiateMaterialDistributionResponse
            {
                GroupId = request.GroupId,
                DistributionsCreated = distributions.Count,
                Distributions = distributions.Select(d =>
                {
                    var plotCultivation = plotCultivationDict[d.PlotCultivationId];
                    var plot = plotDict[plotCultivation.PlotId];
                    var farmer = farmerDict[plot.FarmerId];
                    var material = materialDict[d.MaterialId];

                    return new MaterialDistributionDto
                    {
                        Id = d.Id,
                        PlotCultivationId = d.PlotCultivationId,
                        PlotName =  "Unknown",
                        FarmerName = farmer.FullName ?? "Unknown",
                        MaterialId = d.MaterialId,
                        MaterialName = material.Name,
                        Quantity = d.QuantityDistributed,
                        Status = d.Status.ToString(),
                        ScheduledDistributionDate = d.ScheduledDistributionDate,
                        DistributionDeadline = d.DistributionDeadline,
                        SupervisorConfirmationDeadline = d.SupervisorConfirmationDeadline
                    };
                }).ToList()
            };

            return Result<InitiateMaterialDistributionResponse>.Success(
                response,
                $"Successfully initiated {distributions.Count} material distributions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating material distribution for group {GroupId}", request.GroupId);
            return Result<InitiateMaterialDistributionResponse>.Failure($"Error initiating material distribution: {ex.Message}");
        }
    }

    private async Task<DistributionSettings> GetDistributionSettings()
    {
        var daysBeforeTask = await GetSystemSettingInt("MaterialDistributionDaysBeforeTask", 7);
        var supervisorWindow = await GetSystemSettingInt("SupervisorConfirmationWindowDays", 2);
        var farmerWindow = await GetSystemSettingInt("FarmerConfirmationWindowDays", 3);

        return new DistributionSettings
        {
            DaysBeforeTask = daysBeforeTask,
            SupervisorConfirmationWindow = supervisorWindow,
            FarmerConfirmationWindow = farmerWindow
        };
    }

    private async Task<int> GetSystemSettingInt(string key, int defaultValue)
    {
        var setting = await _unitOfWork.Repository<SystemSetting>()
            .FindAsync(s => s.SettingKey == key);

        return setting != null && int.TryParse(setting.SettingValue, out var value)
            ? value
            : defaultValue;
    }
}

public class DistributionSettings
{
    public int DaysBeforeTask { get; set; }
    public int SupervisorConfirmationWindow { get; set; }
    public int FarmerConfirmationWindow { get; set; }
}

