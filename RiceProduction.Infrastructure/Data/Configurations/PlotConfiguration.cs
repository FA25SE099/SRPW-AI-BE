namespace RiceProduction.Infrastructure.Data.Configurations;

public class PlotConfiguration : IEntityTypeConfiguration<Plot>
{
    public void Configure(EntityTypeBuilder<Plot> builder)
    { 

        builder.Property(p => p.Boundary)
            .IsRequired()
            .HasColumnType("geometry(Polygon,4326)");

        builder.Property(p => p.Area)
            .IsRequired()
            .HasColumnType("decimal(10,2)");

        builder.Property(p => p.SoilType)
            .HasMaxLength(100);

        builder.Property(p => p.Coordinate)
            .HasColumnType("geometry(Point,4326)");

        builder.Property(p => p.Status)
            .HasConversion<string>();

        // Indexes
        builder.HasIndex(p => p.FarmerId);
        builder.HasIndex(p => p.GroupId);
        builder.HasIndex(p => p.Boundary)
            .HasMethod("GIST");
        builder.HasIndex(p => p.Coordinate)
            .HasMethod("GIST");
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.SoilType);

        // Relationships
        builder.HasOne(p => p.Farmer)
            .WithMany(f => f.OwnedPlots)
            .HasForeignKey(p => p.FarmerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Group)
            .WithMany(g => g.Plots)
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}