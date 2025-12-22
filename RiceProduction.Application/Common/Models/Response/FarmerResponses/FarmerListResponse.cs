namespace RiceProduction.Application.Common.Models.Response.FarmerResponses;

public class FarmerListResponse
{
    public Guid FarmerId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FarmCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public string FarmerStatus { get; set; } = string.Empty; // Normal, Warned, NotAllowed, Resigned
    public DateTime? LastActivityAt { get; set; }
    public Guid? ClusterId { get; set; }
    public string? ClusterName { get; set; }
    public int PlotCount { get; set; }
}
