//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using RiceProduction.Application.Common.Interfaces;
//using RiceProduction.Application.Common.Interfaces.External;
//using RiceProduction.Application.Common.Models;
//using RiceProduction.Domain.Entities;
//using System;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace RiceProduction.Application.ReportFeature.Queries.GetAIRecommendations;

//public class GetAIRecommendationsQueryHandler 
//    : IRequestHandler<GetAIRecommendationsQuery, Result<EmergencyPlanRecommendation>>
//{
//    private readonly IUnitOfWork _unitOfWork;
//    private readonly IGeminiAIService _geminiAIService;
//    private readonly ILogger<GetAIRecommendationsQueryHandler> _logger;

//    public GetAIRecommendationsQueryHandler(
//        IUnitOfWork unitOfWork,
//        IGeminiAIService geminiAIService,
//        ILogger<GetAIRecommendationsQueryHandler> logger)
//    {
//        _unitOfWork = unitOfWork;
//        _geminiAIService = geminiAIService;
//        _logger = logger;
//    }

//    public async Task<Result<EmergencyPlanRecommendation>> Handle(
//        GetAIRecommendationsQuery request,
//        CancellationToken cancellationToken)
//    {
//        try
//        {
//            // 1. Load the emergency report with necessary data
//            var report = await _unitOfWork.Repository<EmergencyReport>()
//                .GetQueryable()
//                .Include(r => r.PlotCultivation)
//                    .ThenInclude(pc => pc.Plot)
//                .Include(r => r.PlotCultivation)
//                    .ThenInclude(pc => pc.RiceVariety)
//                .Include(r => r.PlotCultivation)
//                    .ThenInclude(pc => pc.Season)
//                .FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);

//            if (report == null)
//            {
//                return Result<EmergencyPlanRecommendation>.Failure(
//                    $"Report with ID {request.ReportId} not found.",
//                    "NotFound");
//            }

//            // 2. Build request for Gemini AI
//            var aiRequest = new EmergencyPlanRequest
//            {
//                AlertType = report.AlertType,
//                Title = report.Title,
//                Description = report.Description,
//                Severity = report.Severity.ToString(),
//                PlotArea = report.PlotCultivation?.Plot?.Area ?? 1.0m,
//                RiceVariety = report.PlotCultivation?.RiceVariety?.VarietyName,
//                CurrentGrowthStage = report.PlotCultivation?.Season?.SeasonName,
//                DetectedPests = report.AiDetectedPestNames,
//                AiConfidence = report.AiAverageConfidence
//            };

//            _logger.LogInformation(
//                "Requesting AI recommendations for report {ReportId}. Alert: {AlertType}, Severity: {Severity}",
//                report.Id, report.AlertType, report.Severity);

//            // 3. Call Gemini AI service
//            var recommendation = await _geminiAIService.GenerateEmergencyPlanAsync(aiRequest);

//            _logger.LogInformation(
//                "AI recommendations generated successfully for report {ReportId}. Tasks: {TaskCount}",
//                report.Id, recommendation.Tasks.Count);

//            return Result<EmergencyPlanRecommendation>.Success(
//                recommendation,
//                "AI recommendations generated successfully.");
//        }
//        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
//        {
//            _logger.LogError(ex, "Gemini API key not configured");
//            return Result<EmergencyPlanRecommendation>.Failure(
//                "AI service is not configured. Please contact administrator.",
//                "ConfigurationError");
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error generating AI recommendations for report {ReportId}", request.ReportId);
//            return Result<EmergencyPlanRecommendation>.Failure(
//                "An error occurred while generating AI recommendations. Please try again or create plan manually.",
//                "AIGenerationFailed");
//        }
//    }
//}

