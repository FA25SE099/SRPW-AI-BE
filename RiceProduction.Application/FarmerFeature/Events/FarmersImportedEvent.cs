using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Events;

public class FarmersImportedEvent : INotification
{
    public List<ImportedFarmerData> ImportedFarmers { get; set; } = new();
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

