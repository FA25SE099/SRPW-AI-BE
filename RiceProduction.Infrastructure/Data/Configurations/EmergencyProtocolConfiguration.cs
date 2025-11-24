using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class EmergencyProtocolConfiguration : IEntityTypeConfiguration<EmergencyProtocol>
{
    public void Configure(EntityTypeBuilder<EmergencyProtocol> builder)
    {
        builder.ToTable("EmergencyProtocols");

        builder.HasMany(e => e.Thresholds)
            .WithOne(t => t.EmergencyProtocol)
            .HasForeignKey(t => t.EmergencyProtocolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}