using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Request.ThresholdRequests;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Commands.UpdateEmergencyProtocol;

public class UpdateEmergencyProtocolCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid EmergencyProtocolId { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int TotalDurationDays { get; set; }

    public bool IsActive { get; set; } = true;

    public List<StandardPlanStageRequest> Stages { get; set; } = new();

    public List<ThresholdRequest> Thresholds { get; set; } = new();
}