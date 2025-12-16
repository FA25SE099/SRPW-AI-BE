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

        builder.Property(g => g.Year)
            .IsRequired()
            .HasComment("Year of the season cycle to distinguish recurring seasons");

        builder.HasIndex(g => g.ClusterId);
        builder.HasIndex(g => g.SupervisorId);
        builder.HasIndex(g => g.RiceVarietyId);
        builder.HasIndex(g => g.SeasonId);
        builder.HasIndex(g => g.YearSeasonId);
        
        builder.HasIndex(g => new { g.SupervisorId, g.SeasonId, g.Year })
            .HasDatabaseName("IX_Group_Supervisor_Season_Year");
        
        builder.HasIndex(g => g.Status);
        builder.HasIndex(g => g.Area)
            .HasMethod("GIST");
        builder.HasIndex(g => g.PlantingDate);
        builder.HasIndex(g => g.ReadyForUavDate);

        builder.HasOne(g => g.Cluster)
            .WithMany(c => c.Groups)
            .HasForeignKey(g => g.ClusterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Season)
            .WithMany()
            .HasForeignKey(g => g.SeasonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(g => g.YearSeason)
            .WithMany(ys => ys.Groups)
            .HasForeignKey(g => g.YearSeasonId)
            .OnDelete(DeleteBehavior.SetNull);

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