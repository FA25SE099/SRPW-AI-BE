namespace RiceProduction.Infrastructure.Data.Configurations;

public class UavVendorConfiguration : IEntityTypeConfiguration<UavVendor>
{
    public void Configure(EntityTypeBuilder<UavVendor> builder)
    {
        builder.Property(uv => uv.VendorName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(uv => uv.BusinessRegistrationNumber)
            .HasMaxLength(100);

        builder.Property(uv => uv.ServiceRatePerHa)
            .IsRequired()
            .HasColumnType("decimal(10,2)");

        builder.Property(uv => uv.ServiceRadius)
            .HasColumnType("decimal(8,2)");

        builder.Property(uv => uv.EquipmentSpecs)
            .HasColumnType("jsonb");

        builder.Property(uv => uv.OperatingSchedule)
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(uv => uv.VendorName);
        builder.HasIndex(uv => uv.ServiceRatePerHa);
        builder.HasIndex(uv => uv.ServiceRadius);
        builder.HasIndex(uv => uv.FleetSize);
    }
}