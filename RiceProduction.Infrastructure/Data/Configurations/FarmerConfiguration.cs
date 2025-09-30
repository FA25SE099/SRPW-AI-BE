namespace RiceProduction.Infrastructure.Data.Configurations;

public class FarmerConfiguration : IEntityTypeConfiguration<Farmer>
{
    public void Configure(EntityTypeBuilder<Farmer> builder)
    {
        builder.Property(f => f.FarmCode)
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(f => f.FarmCode);
    }
}