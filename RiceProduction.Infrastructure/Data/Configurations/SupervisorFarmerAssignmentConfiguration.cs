namespace RiceProduction.Infrastructure.Data.Configurations;

public class SupervisorFarmerAssignmentConfiguration : IEntityTypeConfiguration<SupervisorFarmerAssignment>
{
    public void Configure(EntityTypeBuilder<SupervisorFarmerAssignment> builder)
    {
        builder.Property(sfa => sfa.AssignmentNotes)
            .HasMaxLength(1000);

        // Indexes
        builder.HasIndex(sfa => sfa.SupervisorId);
        builder.HasIndex(sfa => sfa.FarmerId);
        builder.HasIndex(sfa => sfa.IsActive);
        builder.HasIndex(sfa => sfa.AssignedAt);
        builder.HasIndex(sfa => new { sfa.SupervisorId, sfa.FarmerId })
            .IsUnique();

        // Relationships
        builder.HasOne(sfa => sfa.Supervisor)
            .WithMany(s => s.SupervisorAssignments)
            .HasForeignKey(sfa => sfa.SupervisorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sfa => sfa.Farmer)
            .WithMany(f => f.FarmerAssignments)
            .HasForeignKey(sfa => sfa.FarmerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}