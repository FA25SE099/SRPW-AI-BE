namespace RiceProduction.Infrastructure.Data.Configurations;

public class ClusterConfiguration : IEntityTypeConfiguration<Cluster>
{
    public void Configure(EntityTypeBuilder<Cluster> builder)
    {
        builder.Property(c => c.ClusterName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.Boundary)
            .HasColumnType("geometry(Polygon,4326)");

        builder.Property(c => c.Area)
            .HasColumnType("decimal(10,2)");

        // Indexes
        builder.HasIndex(c => c.ClusterName);
        builder.HasIndex(c => c.Boundary)
            .HasMethod("GIST");
        builder.HasIndex(c => c.ClusterManagerId);

    }
}