using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.ThresholdRequests;

public class ThresholdRequest
{
    // Both can be set at the same time
    public Guid? PestProtocolId { get; set; }

    public Guid? WeatherProtocolId { get; set; }

    // === PEST THRESHOLD LIMITS ===

    [MaxLength(100)]
    public string? PestAffectType { get; set; }

    [MaxLength(50)]
    public string? PestSeverityLevel { get; set; }

    public decimal? PestAreaThresholdPercent { get; set; }

    [MaxLength(200)]
    public string? PestPopulationThreshold { get; set; }

    public decimal? PestDamageThresholdPercent { get; set; }

    [MaxLength(100)]
    public string? PestGrowthStage { get; set; }

    [MaxLength(500)]
    public string? PestThresholdNotes { get; set; }

    // === WEATHER THRESHOLD LIMITS ===

    [MaxLength(100)]
    public string? WeatherEventType { get; set; }

    [MaxLength(50)]
    public string? WeatherIntensityLevel { get; set; }

    public decimal? WeatherMeasurementThreshold { get; set; }

    [MaxLength(50)]
    public string? WeatherMeasurementUnit { get; set; }

    [MaxLength(10)]
    public string? WeatherThresholdOperator { get; set; }

    public int? WeatherDurationDaysThreshold { get; set; }

    [MaxLength(500)]
    public string? WeatherThresholdNotes { get; set; }

    // === COMMON FIELDS ===

    [MaxLength(100)]
    public string? ApplicableSeason { get; set; }

    public Guid? RiceVarietyId { get; set; }

    public int? Priority { get; set; }

    [MaxLength(1000)]
    public string? GeneralNotes { get; set; }
}
