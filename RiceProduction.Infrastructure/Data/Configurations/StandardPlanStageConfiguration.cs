using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class StandardPlanStageConfiguration : IEntityTypeConfiguration<StandardPlanStage>
{
    public void Configure(EntityTypeBuilder<StandardPlanStage> builder)
    {
        builder.HasKey(sps => sps.Id);

        builder.HasOne(sps => sps.StandardPlan)
            .WithMany(sp => sp.StandardPlanStages)
            .HasForeignKey(sps => sps.StandardPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ensure no duplicate stage per plan
        builder.Property(sps => sps.Notes)
            .HasMaxLength(1000);

        builder.Property(sps => sps.SequenceOrder)
            .IsRequired();
    }
}

