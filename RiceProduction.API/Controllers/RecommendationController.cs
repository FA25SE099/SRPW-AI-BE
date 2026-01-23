using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using System;
using System.Threading.Tasks;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationController : ControllerBase
{
    private readonly IGeminiRecommendationService _recommendationService;
    private readonly ILogger<RecommendationController> _logger;

    public RecommendationController(
        IGeminiRecommendationService recommendationService,
        ILogger<RecommendationController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    [HttpPost("pest-solution")]
    public async Task<ActionResult<Result<PestRecommendationResponse>>> GetPestRecommendation(
        [FromBody] PestRecommendationRequest request)
    {
        try
        {
            _logger.LogInformation("Received pest recommendation request for {PestCount} pests", 
                request.DetectedPests.Count);

            var result = await _recommendationService.GetRecommendationAsync(request);

            if (!result.Success)
            {
                return BadRequest(Result<PestRecommendationResponse>.Failure(
                    result.Warnings.ToArray()));
            }

            return Ok(Result<PestRecommendationResponse>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pest recommendation request");
            return StatusCode(500, Result<PestRecommendationResponse>.Failure(
                "An error occurred while processing your request"));
        }
    }

    [HttpGet("test-connection")]
    public async Task<ActionResult<Result<bool>>> TestGeminiConnection()
    {
        try
        {
            var isConnected = await _recommendationService.TestConnectionAsync();
            
            if (isConnected)
            {
                return Ok(Result<bool>.Success(true, "Gemini API connection successful"));
            }
            
            return BadRequest(Result<bool>.Failure("Failed to connect to Gemini API"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Gemini connection");
            return StatusCode(500, Result<bool>.Failure($"Connection test failed: {ex.Message}"));
        }
    }
}
