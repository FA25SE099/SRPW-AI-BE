using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Command.UpdateFarmer
{
    public class UpdateFarmerCommand : IRequest<Result<Guid>>
    {
        public Guid FarmerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? FarmCode { get; set; }
        
        // Note: PhoneNumber is NOT updateable as it's the username/identity key
    }
}

