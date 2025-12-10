using MediatR;
using Microsoft.AspNetCore.Http;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;

public class CreateEmergencyReportCommand : IRequest<Result<Guid>>
{
    public Guid? PlotCultivationId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? ClusterId { get; set; }
    public string AlertType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

    public List<IFormFile>? Images { get; set; }

    /// <summary>
    /// AI-detected pest information (pre-analyzed before submission).
    /// Optional: Only provided if user ran pest detection API first.
    /// Frontend should call /api/rice/check-pest before creating report.
    /// </summary>
    public PestDetectionSummary? AiDetectionResult { get; set; }
}

/// <summary>
/// Summary of AI pest detection results from /api/rice/check-pest endpoint
/// </summary>
public class PestDetectionSummary
{
    public bool HasPest { get; set; }
    public int TotalDetections { get; set; }
    public List<DetectedPestInfo> DetectedPests { get; set; } = new();
    public double AverageConfidence { get; set; }
    
    /// <summary>
    /// Image dimensions from AI analysis
    /// </summary>
    public ImageDimensions? ImageInfo { get; set; }
}

/// <summary>
/// Individual pest detection information
/// </summary>
public class DetectedPestInfo
{
    public string PestName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string ConfidenceLevel { get; set; } = string.Empty;
}

/// <summary>
/// Image dimensions from AI analysis
/// </summary>
public class ImageDimensions
{
    public int Width { get; set; }
    public int Height { get; set; }
}