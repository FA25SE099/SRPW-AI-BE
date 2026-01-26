using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Services;

public class ContextualGeminiAIService : IContextualAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContextualGeminiAIService> _logger;

    public ContextualGeminiAIService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ContextualGeminiAIService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ContextualPlanSuggestions> GenerateContextualSuggestionsAsync(ContextualPlanRequest request)
    {
        try
        {
            var apiKey = _configuration["GeminiAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Gemini API key is not configured. Please add 'GeminiAI:ApiKey' to appsettings.json");
            }

            var prompt = BuildContextualPrompt(request);
            var geminiRequest = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = prompt }
                        }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.7,
                    MaxOutputTokens = 10000, // Balanced for detailed responses while ensuring JSON completion
                    ResponseMimeType = "application/json"
                }
            };

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            _logger.LogInformation("Sending contextual AI request for alert: {AlertType} with {TaskCount} existing tasks", 
                request.AlertType, request.ExistingTasks.Count);

            var response = await httpClient.PostAsJsonAsync(endpoint, geminiRequest, jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new Exception($"Gemini API error: {response.StatusCode} - {errorContent}");
            }

            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(jsonOptions);
            
            if (geminiResponse?.Candidates == null || !geminiResponse.Candidates.Any())
            {
                throw new Exception("Gemini API returned no candidates");
            }

            var candidate = geminiResponse.Candidates[0];
            var generatedText = candidate.Content.Parts[0].Text;
            
            // Check if response was truncated
            if (candidate.FinishReason == "MAX_TOKENS" || candidate.FinishReason == "LENGTH")
            {
                _logger.LogWarning("AI response was truncated due to token limit. FinishReason: {FinishReason}", candidate.FinishReason);
            }
            
            _logger.LogInformation("Contextual AI response received. Length: {Length} characters, FinishReason: {FinishReason}", 
                generatedText?.Length ?? 0, candidate.FinishReason ?? "UNKNOWN");

            // Log the raw response for debugging
            _logger.LogDebug("Raw AI response: {Response}", generatedText);

            // Parse the JSON response from Gemini
            ContextualPlanSuggestions? suggestions;
            try
            {
                suggestions = JsonSerializer.Deserialize<ContextualPlanSuggestions>(generatedText, jsonOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse AI response as JSON. Response preview: {Preview}", 
                    generatedText?.Substring(0, Math.Min(500, generatedText?.Length ?? 0)));
                throw new Exception($"AI returned invalid JSON: {jsonEx.Message}. This may be due to truncated response or malformed output.", jsonEx);
            }

            if (suggestions == null)
            {
                throw new Exception("Failed to parse Gemini AI response - result was null");
            }

            // Post-process: Map material names to IDs from available materials
            MapMaterialIdsToSuggestions(suggestions, request.AvailableMaterials);

            _logger.LogInformation("Successfully generated {SuggestionCount} contextual suggestions", suggestions.Suggestions.Count);
            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contextual suggestions with Gemini AI");
            throw;
        }
    }

    private string BuildContextualPrompt(ContextualPlanRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an agricultural expert reviewing an emergency response plan for rice cultivation.");
        sb.AppendLine("The expert has already started creating a plan, and you need to provide SPECIFIC, INCREMENTAL suggestions to improve it.");
        sb.AppendLine();
        sb.AppendLine("=== EMERGENCY SITUATION ===");
        sb.AppendLine($"Alert Type: {request.AlertType}");
        sb.AppendLine($"Title: {request.Title}");
        sb.AppendLine($"Description: {request.Description}");
        sb.AppendLine($"Severity: {request.Severity}");
        sb.AppendLine($"Plot Area: {request.PlotArea} hectares");
        
        if (!string.IsNullOrEmpty(request.RiceVariety))
            sb.AppendLine($"Rice Variety: {request.RiceVariety}");
        
        if (!string.IsNullOrEmpty(request.CurrentGrowthStage))
            sb.AppendLine($"Growth Stage: {request.CurrentGrowthStage}");

        if (request.DetectedPests != null && request.DetectedPests.Any())
        {
            sb.AppendLine($"AI-Detected Pests: {string.Join(", ", request.DetectedPests)}");
            if (request.AiConfidence.HasValue)
                sb.AppendLine($"Detection Confidence: {request.AiConfidence.Value:F2}%");
        }

        sb.AppendLine();
        sb.AppendLine("=== CURRENT PLAN (What expert has so far) ===");
        
        if (!string.IsNullOrEmpty(request.CurrentVersionName))
            sb.AppendLine($"Version Name: {request.CurrentVersionName}");
        
        if (request.ExistingTasks.Any())
        {
            sb.AppendLine($"Existing Tasks ({request.ExistingTasks.Count}):");
            for (int i = 0; i < request.ExistingTasks.Count; i++)
            {
                var task = request.ExistingTasks[i];
                sb.AppendLine($"  Task {i + 1}: {task.TaskName}");
                sb.AppendLine($"    Type: {task.TaskType}");
                sb.AppendLine($"    Order: {task.ExecutionOrder}");
                if (task.ScheduledEndDate.HasValue)
                    sb.AppendLine($"    Scheduled: {task.ScheduledEndDate:yyyy-MM-dd}");
                
                if (task.Materials.Any())
                {
                    sb.AppendLine($"    Materials:");
                    foreach (var mat in task.Materials)
                    {
                        sb.AppendLine($"      - {mat.MaterialName}: {mat.QuantityPerHa} {mat.Unit}/ha");
                    }
                }
                else
                {
                    sb.AppendLine($"    Materials: None");
                }
            }
        }
        else
        {
            sb.AppendLine("NO EXISTING TASKS - Expert hasn't created any tasks yet.");
        }

        sb.AppendLine();
        sb.AppendLine("=== AVAILABLE MATERIALS IN DATABASE ===");
        
        if (request.AvailableMaterials.Any())
        {
            // Group by type for better organization
            var materialsByType = request.AvailableMaterials
                .GroupBy(m => m.MaterialType)
                .OrderBy(g => g.Key);
            
            sb.AppendLine($"Total: {request.AvailableMaterials.Count} materials");
            sb.AppendLine("IMPORTANT: You MUST ONLY suggest materials from this list!");
            sb.AppendLine();
            
            foreach (var typeGroup in materialsByType)
            {
                sb.AppendLine($"{typeGroup.Key}:");
                foreach (var mat in typeGroup.OrderBy(m => m.MaterialName))
                {
                    var mfg = string.IsNullOrEmpty(mat.Manufacturer) ? "" : $" ({mat.Manufacturer})";
                    sb.AppendLine($"  - {mat.MaterialName} [{mat.Unit}]{mfg}");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("WARNING: No materials available in database!");
        }

        sb.AppendLine();
        sb.AppendLine("=== YOUR TASK ===");
        sb.AppendLine("Analyze the current plan and provide SPECIFIC, ACTIONABLE suggestions to improve it.");
        sb.AppendLine("Each suggestion should be INDEPENDENT and APPLY-ABLE separately.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT FOR DESCRIPTIONS:");
        sb.AppendLine("- When referring to tasks, ALWAYS include the task name, not just 'Task X'");
        sb.AppendLine("- Format: 'Thêm [material] vào [TaskName]' instead of 'Thêm [material] vào Task X'");
        sb.AppendLine("- Example: 'Thêm Amino Gold vào Phun thuốc đợt 1' instead of 'Thêm Amino Gold vào Task 2'");
        sb.AppendLine();
        sb.AppendLine("Generate a JSON response with this EXACT structure (use camelCase for ALL keys and enum values):");
        sb.AppendLine(@"{
  ""overallAssessment"": ""Brief assessment of the current plan (2-3 sentences in Vietnamese)"",
  ""suggestions"": [
    {
      ""type"": ""addMaterial"",
      ""priority"": ""High"",
      ""description"": ""Thêm Amino Gold 500ml/ha vào Phun thuốc đợt 1"",
      ""rationale"": ""Amino Gold giúp cây phục hồi nhanh sau khi bị rầy tấn công"",
      ""action"": {
        ""targetTaskIndex"": 1,
        ""targetTaskName"": ""Phun thuốc đợt 1"",
        ""newMaterialName"": ""Amino Gold"",
        ""newQuantityPerHa"": 500,
        ""newUnit"": ""ml"",
        ""materialPurpose"": ""Phục hồi cây lúa"",
        ""alternativeMaterials"": [""Rong biển"", ""Atonik""]
      }
    },
    {
      ""type"": ""modifyMaterialQuantity"",
      ""priority"": ""Critical"",
      ""description"": ""Tăng Villa Fuji từ 1000ml lên 1200ml/ha trong Phun thuốc đợt 1"",
      ""rationale"": ""Mức độ nghiêm trọng cao cần liều lượng cao hơn"",
      ""action"": {
        ""targetTaskIndex"": 1,
        ""targetMaterialName"": ""Villa Fuji"",
        ""currentQuantity"": 1000,
        ""recommendedQuantity"": 1200
      }
    },
    {
      ""type"": ""addTask"",
      ""priority"": ""High"",
      ""description"": ""Thêm task giám sát sau 3 ngày"",
      ""rationale"": ""Cần theo dõi hiệu quả xử lý"",
      ""action"": {
        ""newTaskName"": ""Giám sát hiệu quả"",
        ""newTaskDescription"": ""Kiểm tra mật độ rầy và tình trạng cây"",
        ""newTaskType"": ""PestControl"",
        ""insertAfterTaskIndex"": 1,
        ""newExecutionOrder"": 3,
        ""daysFromNow"": 3,
        ""newTaskMaterials"": []
      }
    }
  ],
  ""generalAdvice"": [
    ""Phun vào buổi chiều tối"",
    ""Theo dõi dự báo thời tiết""
  ],
  ""warnings"": [
    ""Đeo đồ bảo hộ khi phun thuốc"",
    ""Không phun khi trời mưa""
  ]
}");

        sb.AppendLine();
        sb.AppendLine("=== SUGGESTION TYPES ===");
        sb.AppendLine("CRITICAL: Use these EXACT type values (case-sensitive, camelCase):");
        sb.AppendLine("- addMaterial: Suggest adding a material to an existing task");
        sb.AppendLine("- modifyMaterialQuantity: Change quantity of existing material");
        sb.AppendLine("- removeMaterial: Remove unnecessary material");
        sb.AppendLine("- replaceMaterial: Replace one material with another");
        sb.AppendLine("- addTask: Suggest adding a new task");
        sb.AppendLine("- modifyTask: Change task name/description/type");
        sb.AppendLine("- removeTask: Suggest removing a task");
        sb.AppendLine("- reorderTasks: Change task execution order");
        sb.AppendLine("- changeSchedule: Adjust task timing");
        sb.AppendLine("- addTaskDescription: Add details to task");
        sb.AppendLine("- changeTaskType: Change task category");
        sb.AppendLine();
        sb.AppendLine("=== GUIDELINES ===");
        sb.AppendLine("1. Provide MAXIMUM 5 suggestions, prioritize quality over quantity");
        sb.AppendLine("2. Prioritize: Critical > High > Medium > Low");
        sb.AppendLine("3. Use Vietnamese for descriptions and rationale");
        sb.AppendLine("4. Each suggestion must be independently applicable");
        sb.AppendLine("5. Reference specific task indices (0-based) in action.targetTaskIndex");
        sb.AppendLine("6. CRITICAL: In descriptions, use TASK NAMES not 'Task X'. Example: 'Phun thuốc đợt 1' not 'Task 2'");
        sb.AppendLine("7. CRITICAL: Only suggest materials from the AVAILABLE MATERIALS list above");
        sb.AppendLine("8. Use exact material names from the database (case-sensitive)");
        sb.AppendLine("9. Match the correct unit for each material");
        sb.AppendLine("10. If plan is empty, suggest creating complete tasks");
        sb.AppendLine("11. If plan is good, suggest minor improvements");
        sb.AppendLine("12. Focus on emergency response effectiveness");
        sb.AppendLine();
        sb.AppendLine("=== JSON OUTPUT REQUIREMENTS ===");
        sb.AppendLine("CRITICAL: Return ONLY valid, complete JSON:");
        sb.AppendLine("- NO text before or after the JSON object");
        sb.AppendLine("- ALL strings must be properly closed with quotes");
        sb.AppendLine("- ALL arrays must be properly closed with ]");
        sb.AppendLine("- ALL objects must be properly closed with }");
        sb.AppendLine("- Escape special characters in Vietnamese text (use \\\" for quotes)");
        sb.AppendLine("- If suggesting addTask with materials, ensure newTaskMaterials array is complete");
        sb.AppendLine("- Keep response within token limit - reduce suggestions if needed to ensure complete JSON");

        return sb.ToString();
    }

    private void MapMaterialIdsToSuggestions(
        ContextualPlanSuggestions suggestions, 
        List<AvailableMaterialContext> availableMaterials)
    {
        // Create lookup dictionary for fast material name -> ID mapping
        var materialLookup = availableMaterials
            .ToDictionary(m => m.MaterialName.Trim(), m => m, StringComparer.OrdinalIgnoreCase);

        foreach (var suggestion in suggestions.Suggestions)
        {
            var action = suggestion.Action;
            
            // Map material ID for AddMaterial and ReplaceMaterial suggestions
            if ((suggestion.Type == SuggestionType.AddMaterial || 
                 suggestion.Type == SuggestionType.ReplaceMaterial) &&
                !string.IsNullOrEmpty(action.NewMaterialName))
            {
                var materialName = action.NewMaterialName.Trim();
                
                if (materialLookup.TryGetValue(materialName, out var material))
                {
                    action.NewMaterialId = material.MaterialId;
                    action.NewUnit = material.Unit; // Ensure correct unit from database
                    
                    _logger.LogInformation(
                        "Mapped material '{MaterialName}' to ID {MaterialId}",
                        materialName, material.MaterialId);
                }
                else
                {
                    // Material not found in database - log warning
                    _logger.LogWarning(
                        "Material '{MaterialName}' suggested by AI not found in database. Available materials: {Count}",
                        materialName, availableMaterials.Count);
                    
                    // Add warning to suggestion
                    if (suggestion.Rationale != null)
                    {
                        suggestion.Rationale += $" [CẢNH BÁO: Vật tư '{materialName}' không có trong cơ sở dữ liệu]";
                    }
                }
            }
            
            // Map alternative materials
            if (action.AlternativeMaterials != null && action.AlternativeMaterials.Any())
            {
                // Filter alternatives to only include materials that exist in database
                var validAlternatives = action.AlternativeMaterials
                    .Where(altName => materialLookup.ContainsKey(altName.Trim()))
                    .ToList();
                
                action.AlternativeMaterials = validAlternatives;
            }
        }
    }
}

// Gemini API models (reuse from GeminiAIService)
internal class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig GenerationConfig { get; set; } = new();
}

internal class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; }

    [JsonPropertyName("responseMimeType")]
    public string ResponseMimeType { get; set; } = string.Empty;
}

internal class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate> Candidates { get; set; } = new();
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent Content { get; set; } = new();
    
    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

