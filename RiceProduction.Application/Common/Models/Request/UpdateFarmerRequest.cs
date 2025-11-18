namespace RiceProduction.Application.Common.Models.Request
{
    public class UpdateFarmerRequest
    {
        public Guid FarmerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? FarmCode { get; set; }
    }
}

