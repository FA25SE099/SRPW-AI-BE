namespace RiceProduction.Infrastructure.Data.Configurations;

public class RiceVarietyConfiguration : IEntityTypeConfiguration<RiceVariety>
{
    public void Configure(EntityTypeBuilder<RiceVariety> builder)
    {
        builder.Property(rv => rv.VarietyName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(rv => rv.CategoryId)
            .IsRequired();

        builder.Property(rv => rv.BaseGrowthDurationDays)
            .HasComment("Base growth duration - actual duration may vary by season");

        builder.Property(rv => rv.BaseYieldPerHectare)
            .HasPrecision(10, 2)
            .HasComment("Base yield per hectare - actual yield may vary by season");

        builder.Property(rv => rv.Description)
            .HasMaxLength(1000);

        builder.Property(rv => rv.Characteristics)
            .HasMaxLength(2000)
            .HasComment("General characteristics of this rice variety");

        builder.Property(rv => rv.IsActive)
            .HasDefaultValue(true)
            .HasComment("Whether this variety is currently active/available for planting");

        // Indexes
        builder.HasIndex(rv => rv.VarietyName)
               .IsUnique()
               .HasDatabaseName("IX_RiceVariety_VarietyName");

        builder.HasIndex(rv => rv.CategoryId)
               .HasDatabaseName("IX_RiceVariety_CategoryId");

        builder.HasIndex(rv => rv.IsActive)
               .HasDatabaseName("IX_RiceVariety_IsActive");

        builder.HasIndex(rv => rv.BaseGrowthDurationDays)
               .HasDatabaseName("IX_RiceVariety_BaseGrowthDuration");

        builder.HasOne(rv => rv.Category)
            .WithMany(c => c.RiceVarieties)
            .HasForeignKey(rv => rv.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}