namespace RiceProduction.Infrastructure.Data.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.Property(s => s.SeasonName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.Property(s => s.EndDate)
            .IsRequired();

        builder.Property(s => s.SeasonType)
            .HasMaxLength(50)
            .HasComment("Type of season (e.g., Wet Season, Dry Season, Winter-Spring)");

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true)
            .HasComment("Whether this season is currently active for planning");

        // Indexes
        builder.HasIndex(s => s.SeasonName)
               .IsUnique()
               .HasDatabaseName("IX_Season_SeasonName");

        builder.HasIndex(s => new { s.StartDate, s.EndDate })
               .HasDatabaseName("IX_Season_DateRange");

        builder.HasIndex(s => s.IsActive)
               .HasDatabaseName("IX_Season_IsActive");

        builder.HasIndex(s => s.SeasonType)
               .HasDatabaseName("IX_Season_SeasonType");

        // Check constraint to ensure EndDate > StartDate
    }
}