using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RiceProduction.Application.ReportFeature.Queries.GetContextualAISuggestions;

public class GetContextualAISuggestionsQueryHandler 
    : IRequestHandler<GetContextualAISuggestionsQuery, Result<ContextualPlanSuggestions>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IContextualAIService _contextualAIService;
    private readonly ILogger<GetContextualAISuggestionsQueryHandler> _logger;

    public GetContextualAISuggestionsQueryHandler(
        IUnitOfWork unitOfWork,
        IContextualAIService contextualAIService,
        ILogger<GetContextualAISuggestionsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _contextualAIService = contextualAIService;
        _logger = logger;
    }

    public async Task<Result<ContextualPlanSuggestions>> Handle(
        GetContextualAISuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Load the emergency report
            var report = await _unitOfWork.Repository<EmergencyReport>()
                .GetQueryable()
                .Include(r => r.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(r => r.PlotCultivation)
                    .ThenInclude(pc => pc.RiceVariety)
                .Include(r => r.PlotCultivation)
                    .ThenInclude(pc => pc.Season)
                .FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);

            if (report == null)
            {
                return Result<ContextualPlanSuggestions>.Failure(
                    $"Report with ID {request.ReportId} not found.",
                    "NotFound");
            }

            // 2. Load the plot cultivation with current active version and tasks
            var plotCultivation = await _unitOfWork.Repository<PlotCultivation>()
                .GetQueryable()
                .Include(pc => pc.Plot)
                .Include(pc => pc.RiceVariety)
                .Include(pc => pc.Season)
                .Include(pc => pc.CultivationVersions)
                .Include(pc => pc.CultivationTasks)
                    .ThenInclude(ct => ct.CultivationTaskMaterials)
                        .ThenInclude(ctm => ctm.Material)
                .FirstOrDefaultAsync(pc => pc.Id == request.PlotCultivationId, cancellationToken);

            if (plotCultivation == null)
            {
                return Result<ContextualPlanSuggestions>.Failure(
                    $"Plot cultivation with ID {request.PlotCultivationId} not found.",
                    "NotFound");
            }

            // 3. Get current active version and its tasks
            var activeVersion = plotCultivation.CultivationVersions
                .FirstOrDefault(v => v.IsActive);

            var existingTasks = plotCultivation.CultivationTasks
                .Where(ct => activeVersion == null || ct.VersionId == activeVersion.Id)
                .OrderBy(ct => ct.ExecutionOrder)
                .Select((ct, index) => new ExistingTaskContext
                {
                    TaskIndex = index,
                    TaskName = ct.CultivationTaskName ?? "Unnamed Task",
                    Description = ct.Description ?? "",
                    TaskType = ct.TaskType?.ToString() ?? "Other",
                    ExecutionOrder = ct.ExecutionOrder ?? index + 1,
                    ScheduledEndDate = ct.ScheduledEndDate,
                    Materials = ct.CultivationTaskMaterials.Select(ctm => new ExistingMaterialContext
                    {
                        MaterialName = ctm.Material?.Name ?? "Unknown Material",
                        QuantityPerHa = ctm.ActualQuantity / (plotCultivation.Plot?.Area ?? 1.0m),
                        Unit = ctm.Material?.Unit ?? "unit"
                    }).ToList()
                })
                .ToList();

            // 4. Load available materials from database
            var availableMaterials = await _unitOfWork.Repository<Material>()
                .GetQueryable()
                .Where(m => m.IsActive)
                .Select(m => new AvailableMaterialContext
                {
                    MaterialId = m.Id,
                    MaterialName = m.Name,
                    MaterialType = m.Type.ToString(),
                    Unit = m.Unit,
                    Manufacturer = m.Manufacturer
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Loaded {MaterialCount} available materials from database",
                availableMaterials.Count);

            // 5. Build contextual AI request
            var aiRequest = new ContextualPlanRequest
            {
                // Emergency context
                AlertType = report.AlertType,
                Title = report.Title,
                Description = report.Description,
                Severity = report.Severity.ToString(),
                PlotArea = plotCultivation.Plot?.Area ?? 1.0m,
                RiceVariety = plotCultivation.RiceVariety?.VarietyName,
                CurrentGrowthStage = plotCultivation.Season?.SeasonName,
                DetectedPests = report.AiDetectedPestNames,
                AiConfidence = report.AiAverageConfidence,
                
                // Current plan context
                CurrentVersionName = activeVersion?.VersionName,
                ExistingTasks = existingTasks,
                
                // Available materials
                AvailableMaterials = availableMaterials
            };

            _logger.LogInformation(
                "Requesting contextual AI suggestions for report {ReportId}. Existing tasks: {TaskCount}, Available materials: {MaterialCount}",
                report.Id, existingTasks.Count, availableMaterials.Count);

            // 6. Call Contextual AI service
            var suggestions = await _contextualAIService.GenerateContextualSuggestionsAsync(aiRequest);

            _logger.LogInformation(
                "Contextual AI suggestions generated successfully for report {ReportId}. Suggestions: {SuggestionCount}",
                report.Id, suggestions.Suggestions.Count);

            return Result<ContextualPlanSuggestions>.Success(
                suggestions,
                "Contextual suggestions generated successfully.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            _logger.LogError(ex, "Gemini API key not configured");
            return Result<ContextualPlanSuggestions>.Failure(
                "AI service is not configured. Please contact administrator.",
                "ConfigurationError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contextual AI suggestions for report {ReportId}", request.ReportId);
            return Result<ContextualPlanSuggestions>.Failure(
                "An error occurred while generating AI suggestions. Please try again or edit plan manually.",
                "AIGenerationFailed");
        }
    }
}

