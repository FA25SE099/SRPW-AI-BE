namespace RiceProduction.Application.Common.Models.Request
{
    public class CreateFarmerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? FarmCode { get; set; }
        
        // Optional multiple plots to create along with farmer
        public List<PlotCreationRequest>? Plots { get; set; }
    }

    public class PlotCreationRequest
    {
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        public decimal PlotArea { get; set; }
        public string? SoilType { get; set; }
    }
}

