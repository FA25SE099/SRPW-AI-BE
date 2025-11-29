using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.EmergencyProtocolResponses;

public class EmergencyProtocolDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int TotalDuration { get; set; }
    public bool IsActive { get; set; }
    public int TotalThresholds { get; set; }
    public int TotalTasks { get; set; }
    public int TotalStages { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public Guid? LastModifiedBy { get; set; }
}

public class EmergencyProtocolDetailDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TotalDurationDays { get; set; }
    public bool IsActive { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid? CreatedBy { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastModified { get; set; }

    // Thresholds
    public List<EmergencyThresholdDetailDto> Thresholds { get; set; } = new();  
    public int TotalThresholds { get; set; }

    // Stages
    public List<EmergencyProtocolStageDetailDto> Stages { get; set; } = new(); 
    public int TotalStages { get; set; }
    public int TotalTasks { get; set; }
    public int TotalMaterialTypes { get; set; }
}

public class EmergencyThresholdDetailDto
{
    public Guid Id { get; set; }

    // Both can be set
    public Guid? PestProtocolId { get; set; }
    public string? PestProtocolName { get; set; }
    public Guid? WeatherProtocolId { get; set; }
    public string? WeatherProtocolName { get; set; }

    // === PEST THRESHOLD LIMITS ===
    public string? PestAffectType { get; set; }
    public string? PestSeverityLevel { get; set; }
    public decimal? PestAreaThresholdPercent { get; set; }
    public string? PestPopulationThreshold { get; set; }
    public decimal? PestDamageThresholdPercent { get; set; }
    public string? PestGrowthStage { get; set; }
    public string? PestThresholdNotes { get; set; }

    // === WEATHER THRESHOLD LIMITS ===
    public string? WeatherEventType { get; set; }
    public string? WeatherIntensityLevel { get; set; }
    public decimal? WeatherMeasurementThreshold { get; set; }
    public string? WeatherMeasurementUnit { get; set; }
    public string? WeatherThresholdOperator { get; set; }
    public int? WeatherDurationDaysThreshold { get; set; }
    public string? WeatherThresholdNotes { get; set; }

    // === COMMON FIELDS ===
    public string? ApplicableSeason { get; set; }
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public int? Priority { get; set; }
    public string? GeneralNotes { get; set; }
}

public class EmergencyProtocolStageDetailDto
{
    public Guid Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public int? ExpectedDurationDays { get; set; }
    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
    public List<EmergencyProtocolTaskDetailDto> Tasks { get; set; } = new(); 
}

public class EmergencyProtocolTaskDetailDto
{
    public Guid Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DaysAfter { get; set; }
    public int DurationDays { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public List<EmergencyProtocolTaskMaterialDetailDto> Materials { get; set; } = new(); 
}

public class EmergencyProtocolTaskMaterialDetailDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public string? Unit { get; set; }
    public decimal? PricePerUnit { get; set; }
    public decimal EstimatedCostPerHa { get; set; }
}