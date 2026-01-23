using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External;

/// <summary>
/// Service for generating AI-powered pest control recommendations using Gemini AI
/// </summary>
public interface IGeminiRecommendationService
{
    /// <summary>
    /// Get AI-powered recommendations for detected pests
    /// </summary>
    /// <param name="request">Request containing detected pests and farm context</param>
    /// <returns>Comprehensive recommendation including protocols and AI insights</returns>
    Task<PestRecommendationResponse> GetRecommendationAsync(PestRecommendationRequest request);

    /// <summary>
    /// Test Gemini API connection
    /// </summary>
    /// <returns>True if connection is successful</returns>
    Task<bool> TestConnectionAsync();
}
