namespace RiceProduction.Application.Common.Models.Response.GroupResponse;
public class GroupUavOrderResponse
{
    public Guid Id { get; set; }
    public string OrderName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public decimal TotalArea { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? VendorName { get; set; }
}