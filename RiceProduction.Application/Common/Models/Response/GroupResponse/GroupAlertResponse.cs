using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.Common.Models.Response.GroupResponse;

public class GroupAlertResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}