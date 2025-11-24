using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Request.ThresholdRequests;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Commands.CreateEmergencyProtocol;

public class CreateEmergencyProtocolCommand : IRequest<Result<Guid>>
{
    /// <summary>
    /// Rice variety category (short, medium, long duration)
    /// </summary>
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Total expected duration from emergency detection to completion (in days)
    /// </summary>
    [Required]
    public int TotalDurationDays { get; set; }

    /// <summary>
    /// Whether this protocol is active and available for use
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// All production stages with their tasks and materials
    /// </summary>
    public List<StandardPlanStageRequest> Stages { get; set; } = new();

    /// <summary>
    /// Thresholds that determine when this protocol should be activated
    /// Each threshold references either a PestProtocol or WeatherProtocol
    /// </summary>
    public List<ThresholdRequest> Thresholds { get; set; } = new();
}