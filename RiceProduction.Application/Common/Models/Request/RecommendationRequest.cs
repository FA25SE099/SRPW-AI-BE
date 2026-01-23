using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request;
public class PestRecommendationRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one detected pest is required")]
    public List<DetectedPestInput> DetectedPests { get; set; } = new();
    public FarmContextInfo? FarmContext { get; set; }
    public string Language { get; set; } = "vi";
}
public class DetectedPestInput
{
    [Required]
    public string PestName { get; set; } = string.Empty;
    [Range(0, 100)]
    public double Confidence { get; set; }
    public int DetectionCount { get; set; } = 1;
    public string? ConfidenceLevel { get; set; }
}
public class FarmContextInfo
{
    public string? Location { get; set; }
    public string? Season { get; set; }
    public string? RiceVariety { get; set; }
    public string? GrowthStage { get; set; }
    public double? FieldArea { get; set; }
    public string? Notes { get; set; }
}
