using System;
using System.Collections.Generic;

namespace RiceProduction.Application.Common.Models.Response;

/// <summary>
/// Response model for AI-powered pest control recommendations
/// </summary>
public class PestRecommendationResponse
{
    /// <summary>
    /// Whether the recommendation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Overall severity assessment
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Summary of detected pests
    /// </summary>
    public List<PestSummary> DetectedPestsSummary { get; set; } = new();

    /// <summary>
    /// Recommended protocols from database
    /// </summary>
    public List<RecommendedProtocol> RecommendedProtocols { get; set; } = new();

    /// <summary>
    /// AI-generated insights and recommendations from Gemini
    /// </summary>
    public GeminiRecommendation AiRecommendation { get; set; } = new();

    /// <summary>
    /// Estimated cost range for treatment
    /// </summary>
    public string? EstimatedCost { get; set; }

    /// <summary>
    /// Recommended timeline for action
    /// </summary>
    public string? Timeline { get; set; }

    /// <summary>
    /// Any errors or warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Summary of a detected pest
/// </summary>
public class PestSummary
{
    public string PestName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int DetectionCount { get; set; }
    public string ConfidenceLevel { get; set; } = string.Empty;
}

/// <summary>
/// A recommended protocol from the database
/// </summary>
public class RecommendedProtocol
{
    public Guid ProtocolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public List<string>? ImageLinks { get; set; }
    public string? Notes { get; set; }
    public int Priority { get; set; }
    public string? MatchReason { get; set; }
}

/// <summary>
/// AI-generated recommendation from Gemini
/// </summary>
public class GeminiRecommendation
{
    /// <summary>
    /// Overall assessment and situation analysis
    /// </summary>
    public string Assessment { get; set; } = string.Empty;

    /// <summary>
    /// Detailed treatment recommendations
    /// </summary>
    public string TreatmentRecommendation { get; set; } = string.Empty;

    /// <summary>
    /// Preventive measures to avoid future infestations
    /// </summary>
    public string PreventiveMeasures { get; set; } = string.Empty;

    /// <summary>
    /// Additional expert insights
    /// </summary>
    public string AdditionalInsights { get; set; } = string.Empty;

    /// <summary>
    /// Full raw response from Gemini (for debugging)
    /// </summary>
    public string? RawResponse { get; set; }
}
