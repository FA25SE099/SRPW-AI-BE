using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Command.CreateFarmer
{
    public class CreateFarmerCommand : IRequest<Result<Guid>>
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? FarmCode { get; set; }
        public string? Email { get; set; }

        // Optional multiple plots to create along with farmer
        public List<PlotCreationData>? Plots { get; set; }
        
        // For cluster manager context
        public Guid? ClusterManagerId { get; set; }
    }

    public class PlotCreationData
    {
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        public decimal PlotArea { get; set; }
        public string? SoilType { get; set; }
    }
}

