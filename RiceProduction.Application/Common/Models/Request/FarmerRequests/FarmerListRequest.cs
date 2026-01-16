namespace RiceProduction.Application.Common.Models.Request.FarmerRequests;

public class FarmerListRequest
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? ClusterId { get; set; }
    public string? FarmerStatus { get; set; } // Normal, Warned, NotAllowed, Resigned
}
