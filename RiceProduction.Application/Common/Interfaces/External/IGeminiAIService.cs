using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External;

/// <summary>
/// AI-powered recommendations for emergency plan creation using Google Gemini
/// </summary>
public interface IGeminiAIService
{
    /// <summary>
    /// Generate emergency plan recommendations based on report details
    /// </summary>
    Task<EmergencyPlanRecommendation> GenerateEmergencyPlanAsync(EmergencyPlanRequest request);
}

/// <summary>
/// Request for AI emergency plan generation
/// </summary>
public class EmergencyPlanRequest
{
    public string AlertType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }
    public string? RiceVariety { get; set; }
    public string? CurrentGrowthStage { get; set; }
    
    /// <summary>
    /// AI-detected pest names from image analysis
    /// </summary>
    public List<string>? DetectedPests { get; set; }
    
    /// <summary>
    /// Confidence level of AI pest detection
    /// </summary>
    public double? AiConfidence { get; set; }
    
    /// <summary>
    /// Available materials from database
    /// </summary>
    public List<AvailableMaterialContext>? AvailableMaterials { get; set; }
}

/// <summary>
/// AI-generated emergency plan recommendation
/// </summary>
public class EmergencyPlanRecommendation
{
    public string RecommendedVersionName { get; set; } = string.Empty;
    public string ResolutionReason { get; set; } = string.Empty;
    public int EstimatedUrgencyHours { get; set; }
    public List<RecommendedTask> Tasks { get; set; } = new();
    public string GeneralAdvice { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// AI-recommended emergency task
/// </summary>
public class RecommendedTask
{
    public string TaskName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty; // PestControl, Fertilization, etc.
    public int ExecutionOrder { get; set; }
    public int DaysFromNow { get; set; }
    public string Priority { get; set; } = string.Empty;
    public List<RecommendedMaterial> Materials { get; set; } = new();
    public string Rationale { get; set; } = string.Empty;
}

/// <summary>
/// AI-recommended material with quantity
/// </summary>
public class RecommendedMaterial
{
    public string MaterialName { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<string>? Alternatives { get; set; }
}

