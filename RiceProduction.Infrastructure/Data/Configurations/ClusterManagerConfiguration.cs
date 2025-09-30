namespace RiceProduction.Infrastructure.Data.Configurations;

public class ClusterManagerConfiguration : IEntityTypeConfiguration<ClusterManager>
{
    public void Configure(EntityTypeBuilder<ClusterManager> builder)
    {
        // One-to-one relationship with Cluster
        builder.HasOne(cm => cm.ManagedCluster)
            .WithOne(c => c.ClusterManager)
            .HasForeignKey<ClusterManager>(cm => cm.ClusterId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(cm => cm.ClusterId)
            .IsUnique();
    }
}