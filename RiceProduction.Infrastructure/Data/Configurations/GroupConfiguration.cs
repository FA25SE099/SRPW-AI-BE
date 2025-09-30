namespace RiceProduction.Infrastructure.Data.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.Property(g => g.Status)
            .HasConversion<string>();

        builder.Property(g => g.Area)
            .HasColumnType("geometry(Polygon,4326)");

        builder.Property(g => g.TotalArea)
            .HasColumnType("decimal(10,2)");

        // Indexes
        builder.HasIndex(g => g.ClusterId);
        builder.HasIndex(g => g.SupervisorId);
        builder.HasIndex(g => g.RiceVarietyId);
        builder.HasIndex(g => g.SeasonId);
        builder.HasIndex(g => g.Status);
        builder.HasIndex(g => g.Area)
            .HasMethod("GIST");
        builder.HasIndex(g => g.PlantingDate);
        builder.HasIndex(g => g.ReadyForUavDate);

        // Relationships
        builder.HasOne(g => g.Cluster)
            .WithMany(c => c.Groups)
            .HasForeignKey(g => g.ClusterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Supervisor)
            .WithMany(s => s.SupervisedGroups)
            .HasForeignKey(g => g.SupervisorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(g => g.RiceVariety)
            .WithMany(rv => rv.Groups)
            .HasForeignKey(g => g.RiceVarietyId)
            .OnDelete(DeleteBehavior.SetNull);

    }
}