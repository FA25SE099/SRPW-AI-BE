using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.EmergencyProtocolRequests;

public class CreateEmergencyProtocolRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int TotalDurationDays { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Reference to pest protocol (which contains threshold information)
    /// </summary>
    public Guid? PestProtocolId { get; set; }

    /// <summary>
    /// Reference to weather protocol (which contains threshold information)
    /// </summary>
    public Guid? WeatherProtocolId { get; set; }

    /// <summary>
    /// Standard production stages with their tasks
    /// </summary>
    public List<StandardPlanStageRequest> Stages { get; set; } = new();
}
