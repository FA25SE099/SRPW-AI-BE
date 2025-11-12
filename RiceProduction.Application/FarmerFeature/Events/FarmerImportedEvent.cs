using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Events;

public class FarmerImportedEvent : INotification
{
    public ImportFarmerResult ImportResult { get; set; } = null!;
    public Guid? ClusterManagerId { get; set; }
    public DateTime ImportedAt { get; set; }
}

