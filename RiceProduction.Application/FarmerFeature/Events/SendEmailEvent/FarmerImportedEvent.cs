using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Events.SendEmailEvent;

public class FarmerImportedEvent : INotification
{
    public class FarmersImportedEvent : INotification
    {
        public List<ImportedFarmerInfo> ImportedFarmers { get; set; } = new();
        public DateTime ImportedAt { get; set; }
    }
    public class ImportedFarmerInfo
    {
        public Guid FarmerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public string? FarmCode { get; set; }
    }
}

