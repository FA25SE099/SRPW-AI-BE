using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;

public class CreateEmergencyReportCommand : IRequest<Result<Guid>>
{
    public Guid? PlotCultivationId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? ClusterId { get; set; }

    /// <summary>
    /// Alert type: "Pest" or "Weather"
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Brief title of the issue (e.g., "Suspicious insect damage", "Heavy rainfall damage")
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Initial description from farmer/supervisor. 
    /// Will be enhanced by AI analysis later if images are provided.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity as perceived by reporter
    /// </summary>
    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

    /// <summary>
    /// Optional: Images for AI analysis to determine pest type or weather damage.
    /// Recommended for pest reports to enable AI identification.
    /// </summary>
    public List<string>? ImageUrls { get; set; }
}
public class CreateEmergencyReportCommand1 : IRequest<Result<Guid>>
{
    public Guid? PlotCultivationId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? ClusterId { get; set; }

    /// <summary>
    /// Alert type: "Pest" or "Weather"
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Brief title of the issue (e.g., "Suspicious insect damage", "Heavy rainfall damage")
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Initial description from farmer/supervisor. 
    /// Will be enhanced by AI analysis later if images are provided.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity as perceived by reporter
    /// </summary>
    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

    /// <summary>
    /// Optional: Images for AI analysis to determine pest type or weather damage.
    /// Recommended for pest reports to enable AI identification.
    /// </summary>
    public List<string>? ImageUrls { get; set; }
}