using Microsoft.EntityFrameworkCore;
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
    private readonly IApplicationDbContext _context;
    private readonly ILogger<InitiateMaterialDistributionCommandHandler> _logger;

    public InitiateMaterialDistributionCommandHandler(
        IApplicationDbContext context,
        ILogger<InitiateMaterialDistributionCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<InitiateMaterialDistributionResponse>> Handle(
        InitiateMaterialDistributionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = await _context.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

            if (group == null)
                return Result<InitiateMaterialDistributionResponse>.Failure("Group not found");

            if (group.Status != GroupStatus.Active)
                return Result<InitiateMaterialDistributionResponse>.Failure("Group must be in Active status");

            var productionPlan = await _context.ProductionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == request.ProductionPlanId && pp.GroupId == request.GroupId, cancellationToken);

            if (productionPlan == null)
                return Result<InitiateMaterialDistributionResponse>.Failure("Production plan not found");

            if (productionPlan.Status != TaskStatus.Approved)
                return Result<InitiateMaterialDistributionResponse>.Failure("Production plan must be approved");

            var settings = await GetDistributionSettings(cancellationToken);

            var distributions = new List<MaterialDistribution>();
            var plotCultivationIds = request.Materials.Select(m => m.PlotCultivationId).Distinct().ToList();
            
            var plotCultivationDict = await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotCultivationIds.Contains(pc.Id))
                .ToDictionaryAsync(pc => pc.Id, cancellationToken);

            var plotIds = plotCultivationDict.Values.Select(pc => pc.PlotId).Distinct().ToList();
            var plotDict = await _context.Plots
                .AsNoTracking()
                .Where(p => plotIds.Contains(p.Id))
                .Select(p => new { p.Id, p.FarmerId })
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var farmerIds = plotDict.Values.Select(p => p.FarmerId).Distinct().ToList();
            var farmerDict = await _context.Farmers
                .AsNoTracking()
                .Where(f => farmerIds.Contains(f.Id))
                .Select(f => new { f.Id, f.FullName })
                .ToDictionaryAsync(f => f.Id, cancellationToken);

            var materialIds = request.Materials.Select(m => m.MaterialId).Distinct().ToList();
            var materialDict = await _context.Materials
                .AsNoTracking()
                .Where(m => materialIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Name })
                .ToDictionaryAsync(m => m.Id, cancellationToken);

            foreach (var item in request.Materials)
            {
                if (!plotCultivationDict.ContainsKey(item.PlotCultivationId))
                    return Result<InitiateMaterialDistributionResponse>.Failure($"PlotCultivation {item.PlotCultivationId} not found");

                if (!materialDict.ContainsKey(item.MaterialId))
                    return Result<InitiateMaterialDistributionResponse>.Failure($"Material {item.MaterialId} not found");

                var existingDistribution = await _context.MaterialDistributions
                    .AsNoTracking()
                    .AnyAsync(md => md.PlotCultivationId == item.PlotCultivationId && 
                                    md.MaterialId == item.MaterialId &&
                                    md.Status != DistributionStatus.Rejected,
                              cancellationToken);

                if (existingDistribution)
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

                _context.MaterialDistributions.Add(distribution);
                distributions.Add(distribution);
            }

            await _context.SaveChangesAsync(cancellationToken);

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
                    farmerDict.TryGetValue(plot.FarmerId, out var farmer);
                    var material = materialDict[d.MaterialId];

                    return new MaterialDistributionDto
                    {
                        Id = d.Id,
                        PlotCultivationId = d.PlotCultivationId,
                        PlotName = "Unknown",
                        FarmerName = farmer?.FullName ?? "Unknown",
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

    private async Task<DistributionSettings> GetDistributionSettings(CancellationToken cancellationToken)
    {
        var daysBeforeTask = await GetSystemSettingInt("MaterialDistributionDaysBeforeTask", 7, cancellationToken);
        var supervisorWindow = await GetSystemSettingInt("SupervisorConfirmationWindowDays", 2, cancellationToken);
        var farmerWindow = await GetSystemSettingInt("FarmerConfirmationWindowDays", 3, cancellationToken);

        return new DistributionSettings
        {
            DaysBeforeTask = daysBeforeTask,
            SupervisorConfirmationWindow = supervisorWindow,
            FarmerConfirmationWindow = farmerWindow
        };
    }

    private async Task<int> GetSystemSettingInt(string key, int defaultValue, CancellationToken cancellationToken)
    {
        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);

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

