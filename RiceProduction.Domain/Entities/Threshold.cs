using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Domain.Entities;
//status: không có trạng thái
/// <summary>
/// Threshold conditions for emergency protocols
/// Can contain BOTH pest and weather thresholds simultaneously
/// </summary>
public class Threshold : BaseAuditableEntity
{
    [Required]
    public Guid EmergencyProtocolId { get; set; }

    // Both can be set at the same time (combined pest + weather condition)
    public Guid? PestProtocolId { get; set; }

    public Guid? WeatherProtocolId { get; set; }

    // === PEST THRESHOLD LIMITS ===

    /// <summary>
    /// Type of pest damage/affect (e.g., "Leaf damage", "Root damage")
    /// </summary>
    [MaxLength(100)]
    public string? PestAffectType { get; set; }

    /// <summary>
    /// Minimum severity level to trigger (e.g., "Medium", "High")
    /// </summary>
    [MaxLength(50)]
    public string? PestSeverityLevel { get; set; }

    /// <summary>
    /// Minimum percentage of area affected to trigger (e.g., 25%)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? PestAreaThresholdPercent { get; set; }

    /// <summary>
    /// Minimum population density to trigger (e.g., "10 insects per plant")
    /// </summary>
    [MaxLength(200)]
    public string? PestPopulationThreshold { get; set; }

    /// <summary>
    /// Minimum damage percentage to trigger (e.g., 30% leaf damage)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? PestDamageThresholdPercent { get; set; }

    /// <summary>
    /// Specific growth stage when this threshold applies
    /// </summary>
    [MaxLength(100)]
    public string? PestGrowthStage { get; set; }

    /// <summary>
    /// Additional notes about pest threshold
    /// </summary>
    [MaxLength(500)]
    public string? PestThresholdNotes { get; set; }

    // === WEATHER THRESHOLD LIMITS ===

    /// <summary>
    /// Type of weather event (e.g., "Rainfall", "Temperature", "Drought")
    /// </summary>
    [MaxLength(100)]
    public string? WeatherEventType { get; set; }

    /// <summary>
    /// Minimum intensity level to trigger (e.g., "Heavy", "Extreme")
    /// </summary>
    [MaxLength(50)]
    public string? WeatherIntensityLevel { get; set; }

    /// <summary>
    /// Minimum measurement value to trigger (e.g., ">150" for 150mm rainfall)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? WeatherMeasurementThreshold { get; set; }

    /// <summary>
    /// Unit of measurement (e.g., "mm/day", "°C", "km/h")
    /// </summary>
    [MaxLength(50)]
    public string? WeatherMeasurementUnit { get; set; }

    public string? WeatherThresholdOperator { get; set; }

    /// <summary>
    /// Minimum duration in days to trigger (e.g., 3 consecutive days)
    /// </summary>
    public int? WeatherDurationDaysThreshold { get; set; }

    /// <summary>
    /// Additional notes about weather threshold
    /// </summary>
    [MaxLength(500)]
    public string? WeatherThresholdNotes { get; set; }

    // === COMMON FIELDS (Apply to both) ===

    /// <summary>
    /// Season when this threshold applies (e.g., "Monsoon", "Dry season")
    /// </summary>
    [MaxLength(100)]
    public string? ApplicableSeason { get; set; }

    /// <summary>
    /// Specific rice variety this threshold applies to (null = all varieties)
    /// </summary>
    public Guid? RiceVarietyId { get; set; }

    /// <summary>
    /// Priority/severity of this threshold (1=Low, 5=Critical)
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// General notes about this threshold combination
    /// </summary>
    [MaxLength(1000)]
    public string? GeneralNotes { get; set; }

    // Navigation properties
    [ForeignKey("EmergencyProtocolId")]
    public EmergencyProtocol EmergencyProtocol { get; set; } = null!;

    [ForeignKey("PestProtocolId")]
    public PestProtocol? PestProtocol { get; set; }

    [ForeignKey("WeatherProtocolId")]
    public WeatherProtocol? WeatherProtocol { get; set; }

    [ForeignKey("RiceVarietyId")]
    public RiceVariety? RiceVariety { get; set; }
}