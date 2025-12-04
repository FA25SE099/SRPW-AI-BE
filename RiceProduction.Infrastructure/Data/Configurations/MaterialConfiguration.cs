namespace RiceProduction.Infrastructure.Data.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(m => m.Unit)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Manufacturer)
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(m => m.Name);
        builder.HasIndex(m => m.Type);
        builder.HasIndex(m => m.IsActive);
        builder.HasIndex(m => m.IsPartition);
        builder.HasIndex(m => m.Manufacturer);
    }
}