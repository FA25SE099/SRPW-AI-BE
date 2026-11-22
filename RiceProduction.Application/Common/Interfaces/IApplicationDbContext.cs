using Microsoft.EntityFrameworkCore;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Farmer> Farmers { get; }
    DbSet<Supervisor> Supervisors { get; }
    DbSet<ClusterManager> ClusterManagers { get; }
    DbSet<Admin> Admins { get; }
    DbSet<AgronomyExpert> AgronomyExperts { get; }
    DbSet<UavVendor> UavVendors { get; }

    DbSet<Cluster> Clusters { get; }
    DbSet<Season> Seasons { get; }
    DbSet<YearSeason> YearSeasons { get; }
    DbSet<RiceVarietyCategory> RiceVarietyCategories { get; }
    DbSet<RiceVariety> RiceVarieties { get; }
    DbSet<RiceVarietySeason> RiceVarietySeasons { get; }
    DbSet<Group> Groups { get; }
    DbSet<Plot> Plots { get; }
    DbSet<GroupPlot> GroupPlots { get; }
    DbSet<PlotCultivation> PlotCultivations { get; }
    DbSet<PlotPolygonTask> PlotPolygonTasks { get; }

    DbSet<Material> Materials { get; }
    DbSet<MaterialPrice> MaterialPrices { get; }
    DbSet<MaterialDistribution> MaterialDistributions { get; }
    DbSet<UavOrderPlotAssignment> UavOrderPlotAssignments { get; }

    DbSet<StandardPlan> StandardPlans { get; }
    DbSet<StandardPlanStage> StandardPlanStages { get; }
    DbSet<StandardPlanTask> StandardPlanTasks { get; }
    DbSet<StandardPlanTaskMaterial> StandardPlanTaskMaterials { get; }
    DbSet<ProductionPlan> ProductionPlans { get; }
    DbSet<ProductionStage> ProductionStages { get; }
    DbSet<ProductionPlanTask> ProductionPlanTasks { get; }
    DbSet<ProductionPlanTaskMaterial> ProductionPlanTaskMaterials { get; }

    DbSet<CultivationTask> CultivationTasks { get; }
    DbSet<CultivationTaskMaterial> CultivationTaskMaterials { get; }
    DbSet<CultivationVersion> CultivationVersions { get; }
    DbSet<FarmLog> FarmLogs { get; }
    DbSet<FarmLogMaterial> FarmLogMaterials { get; }
    DbSet<EmergencyReport> EmergencyReports { get; }
    DbSet<LateFarmerRecord> LateFarmerRecords { get; }

    DbSet<UavServiceOrder> UavServiceOrders { get; }
    DbSet<UavInvoice> UavInvoices { get; }

    DbSet<Alert> Alerts { get; }
    DbSet<FieldWeather> FieldWeathers { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<SupervisorFarmerAssignment> SupervisorFarmerAssignments { get; }

    DbSet<EmergencyProtocol> EmergencyProtocols { get; }
    DbSet<PestProtocol> PestProtocols { get; }
    DbSet<WeatherProtocol> WeatherProtocols { get; }
    DbSet<Threshold> Thresholds { get; }

    DbSet<EmailRequest> EmailRequests { get; }
    DbSet<EmailBatch> EmailBatches { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
