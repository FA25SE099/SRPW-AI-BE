namespace RiceProduction.Infrastructure.Data.Configurations;

public class SupervisorConfiguration : IEntityTypeConfiguration<Supervisor>
{
    public void Configure(EntityTypeBuilder<Supervisor> builder)
    {
       
        // Indexes
        builder.HasIndex(s => s.MaxFarmerCapacity);
        builder.HasIndex(s => s.CurrentFarmerCount);
    }
}