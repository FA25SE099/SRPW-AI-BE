using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Tests.Fixtures;

/// <summary>
/// Lightweight InMemory EF Core implementation of <see cref="IApplicationDbContext"/>
/// for testing Application handlers directly against a real EF Core query provider.
/// </summary>
public sealed class TestApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options)
    {
    }

    public static TestApplicationDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new TestApplicationDbContext(options);
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Farmer> Farmers => Set<Farmer>();
    public DbSet<Supervisor> Supervisors => Set<Supervisor>();
    public DbSet<ClusterManager> ClusterManagers => Set<ClusterManager>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<AgronomyExpert> AgronomyExperts => Set<AgronomyExpert>();
    public DbSet<UavVendor> UavVendors => Set<UavVendor>();

    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<YearSeason> YearSeasons => Set<YearSeason>();
    public DbSet<RiceVarietyCategory> RiceVarietyCategories => Set<RiceVarietyCategory>();
    public DbSet<RiceVariety> RiceVarieties => Set<RiceVariety>();
    public DbSet<RiceVarietySeason> RiceVarietySeasons => Set<RiceVarietySeason>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Plot> Plots => Set<Plot>();
    public DbSet<GroupPlot> GroupPlots => Set<GroupPlot>();
    public DbSet<PlotCultivation> PlotCultivations => Set<PlotCultivation>();
    public DbSet<PlotPolygonTask> PlotPolygonTasks => Set<PlotPolygonTask>();

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialPrice> MaterialPrices => Set<MaterialPrice>();
    public DbSet<MaterialDistribution> MaterialDistributions => Set<MaterialDistribution>();
    public DbSet<UavOrderPlotAssignment> UavOrderPlotAssignments => Set<UavOrderPlotAssignment>();

    public DbSet<StandardPlan> StandardPlans => Set<StandardPlan>();
    public DbSet<StandardPlanStage> StandardPlanStages => Set<StandardPlanStage>();
    public DbSet<StandardPlanTask> StandardPlanTasks => Set<StandardPlanTask>();
    public DbSet<StandardPlanTaskMaterial> StandardPlanTaskMaterials => Set<StandardPlanTaskMaterial>();
    public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
    public DbSet<ProductionStage> ProductionStages => Set<ProductionStage>();
    public DbSet<ProductionPlanTask> ProductionPlanTasks => Set<ProductionPlanTask>();
    public DbSet<ProductionPlanTaskMaterial> ProductionPlanTaskMaterials => Set<ProductionPlanTaskMaterial>();

    public DbSet<CultivationTask> CultivationTasks => Set<CultivationTask>();
    public DbSet<CultivationTaskMaterial> CultivationTaskMaterials => Set<CultivationTaskMaterial>();
    public DbSet<CultivationVersion> CultivationVersions => Set<CultivationVersion>();
    public DbSet<FarmLog> FarmLogs => Set<FarmLog>();
    public DbSet<FarmLogMaterial> FarmLogMaterials => Set<FarmLogMaterial>();
    public DbSet<EmergencyReport> EmergencyReports => Set<EmergencyReport>();
    public DbSet<LateFarmerRecord> LateFarmerRecords => Set<LateFarmerRecord>();

    public DbSet<UavServiceOrder> UavServiceOrders => Set<UavServiceOrder>();
    public DbSet<UavInvoice> UavInvoices => Set<UavInvoice>();

    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<FieldWeather> FieldWeathers => Set<FieldWeather>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SupervisorFarmerAssignment> SupervisorFarmerAssignments => Set<SupervisorFarmerAssignment>();

    public DbSet<EmergencyProtocol> EmergencyProtocols => Set<EmergencyProtocol>();
    public DbSet<PestProtocol> PestProtocols => Set<PestProtocol>();
    public DbSet<WeatherProtocol> WeatherProtocols => Set<WeatherProtocol>();
    public DbSet<Threshold> Thresholds => Set<Threshold>();

    public DbSet<EmailRequest> EmailRequests => Set<EmailRequest>();
    public DbSet<EmailBatch> EmailBatches => Set<EmailBatch>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore all NetTopologySuite geometry properties for the EF Core InMemory provider
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var geometryProps = entityType.ClrType.GetProperties()
                .Where(p => typeof(NetTopologySuite.Geometries.Geometry).IsAssignableFrom(p.PropertyType))
                .ToList();

            foreach (var prop in geometryProps)
            {
                builder.Entity(entityType.ClrType).Ignore(prop.Name);
            }
        }

        // Resolve bidirectional 1:1 relationships for InMemory model building
        builder.Entity<Cluster>()
            .HasOne(c => c.ClusterManager)
            .WithOne(cm => cm.ManagedCluster)
            .HasForeignKey<Cluster>(c => c.ClusterManagerId);

        builder.Entity<Cluster>()
            .HasOne(c => c.AgronomyExpert)
            .WithOne(ae => ae.ManagedCluster)
            .HasForeignKey<Cluster>(c => c.AgronomyExpertId);

        builder.Entity<CultivationTask>()
            .HasOne(t => t.AssignedSupervisor)
            .WithMany(s => s.AssignedTasks)
            .HasForeignKey(t => t.AssignedToUserId);

        builder.Entity<CultivationTask>()
            .HasOne(t => t.Verifier)
            .WithMany()
            .HasForeignKey(t => t.VerifiedBy);
    }
}
