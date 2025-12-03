using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Identity;

namespace RiceProduction.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) 
    {
        // PostgreSQL enums are now mapped at data source level in DependencyInjection.cs
    }

    // Legacy entities
    // Identity entities
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // User hierarchy entities
    public DbSet<Farmer> Farmers => Set<Farmer>();
    public DbSet<Supervisor> Supervisors => Set<Supervisor>();
    public DbSet<ClusterManager> ClusterManagers => Set<ClusterManager>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<AgronomyExpert> AgronomyExperts => Set<AgronomyExpert>();
    public DbSet<UavVendor> UavVendors => Set<UavVendor>();

    // Core entities
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<RiceVarietyCategory> RiceVarietyCategories => Set<RiceVarietyCategory>();
    public DbSet<RiceVariety> RiceVarieties => Set<RiceVariety>();
    public DbSet<RiceVarietySeason> RiceVarietySeasons => Set<RiceVarietySeason>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Plot> Plots => Set<Plot>();
    public DbSet<PlotCultivation> PlotCultivations => Set<PlotCultivation>();

    // Material management
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialPrice> MaterialPrices => Set<MaterialPrice>();

    // Planning entities
    public DbSet<StandardPlan> StandardPlans => Set<StandardPlan>();
    public DbSet<StandardPlanTask> StandardPlanTasks => Set<StandardPlanTask>();
    public DbSet<StandardPlanTaskMaterial> StandardPlanTaskMaterials => Set<StandardPlanTaskMaterial>();
    public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
    public DbSet<ProductionPlanTask> ProductionPlanTasks => Set<ProductionPlanTask>();
    public DbSet<ProductionPlanTaskMaterial> ProductionPlanTaskMaterials => Set<ProductionPlanTaskMaterial>();

    // Production stages
    public DbSet<ProductionStage> ProductionStages => Set<ProductionStage>();
    public DbSet<StandardPlanStage> StandardPlanStages => Set<StandardPlanStage>();

    // Task execution
    public DbSet<CultivationTask> CultivationTasks => Set<CultivationTask>();
    public DbSet<CultivationTaskMaterial> CultivationTaskMaterials => Set<CultivationTaskMaterial>();
    public DbSet<FarmLog> FarmLogs => Set<FarmLog>();
    public DbSet<FarmLogMaterial> FarmLogMaterials => Set<FarmLogMaterial>();
    public DbSet<EmergencyReport> EmergencyReports => Set<EmergencyReport>();
    // UAV services
    public DbSet<UavServiceOrder> UavServiceOrders => Set<UavServiceOrder>();
    public DbSet<UavInvoice> UavInvoices => Set<UavInvoice>();

    // Monitoring and alerts
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<FieldWeather> FieldWeathers => Set<FieldWeather>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    
    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    
    // Assignments
    public DbSet<SupervisorFarmerAssignment> SupervisorFarmerAssignments => Set<SupervisorFarmerAssignment>();

    // Emergencies
    public DbSet<EmergencyProtocol> EmergencyProtocols => Set<EmergencyProtocol>();
    public DbSet<PestProtocol> PestProtocols => Set<PestProtocol>();
    public DbSet<WeatherProtocol> WeatherProtocols => Set<WeatherProtocol>();
    public DbSet<Threshold> Thresholds => Set<Threshold>();
    public DbSet<CultivationVersion> CultivationVersions => Set<CultivationVersion>();

    // Emails
    public DbSet<EmailRequest> EmailRequests => Set<EmailRequest>();
    public DbSet<EmailBatch> EmailBatches => Set<EmailBatch>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure PostgreSQL enums
        builder.HasPostgresEnum<UserRole>();
        builder.HasPostgresEnum<PlotStatus>();
        builder.HasPostgresEnum<GroupStatus>();
        builder.HasPostgresEnum<TaskStatus>();
        builder.HasPostgresEnum<TaskPriority>();
        builder.HasPostgresEnum<TaskType>();
        builder.HasPostgresEnum<MaterialType>();
        builder.HasPostgresEnum<AlertSeverity>();
        builder.HasPostgresEnum<AlertSource>();
        builder.HasPostgresEnum<AlertStatus>();
        builder.HasPostgresEnum<InvoiceStatus>();
        builder.HasPostgresEnum<CultivationStatus>();
        builder.HasPostgresEnum<RiskLevel>();
        builder.HasPostgresEnum<PriorityLevel>();
        builder.HasPostgresExtension("postgis")
            .HasPostgresExtension("uuid-ossp");

        // Configure Table-Per-Type inheritance for user hierarchy
        builder.Entity<ApplicationUser>().ToTable("ApplicationUser");
        builder.Entity<Farmer>().ToTable("Farmers");
        builder.Entity<Supervisor>().ToTable("Supervisors");
        builder.Entity<ClusterManager>().ToTable("ClusterManagers");
        builder.Entity<Admin>().ToTable("Admins");
        builder.Entity<AgronomyExpert>().ToTable("AgronomyExperts");
        builder.Entity<UavVendor>().ToTable("UavVendors");

        // Apply all entity configurations from assembly
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
