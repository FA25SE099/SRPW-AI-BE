//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using RiceProduction.Application.Common.Interfaces.External;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Http;
//using System.Net.Http.Json;
//using System.Text;
//using System.Text.Json;
//using System.Text.Json.Serialization;
//using System.Threading.Tasks;

//namespace RiceProduction.Infrastructure.Services;

//public class GeminiAIService : IGeminiAIService
//{
//    private readonly IHttpClientFactory _httpClientFactory;
//    private readonly IConfiguration _configuration;
//    private readonly ILogger<GeminiAIService> _logger;

//    public GeminiAIService(
//        IHttpClientFactory httpClientFactory,
//        IConfiguration configuration,
//        ILogger<GeminiAIService> logger)
//    {
//        _httpClientFactory = httpClientFactory;
//        _configuration = configuration;
//        _logger = logger;
//    }

//    public async Task<EmergencyPlanRecommendation> GenerateEmergencyPlanAsync(EmergencyPlanRequest request)
//    {
//        try
//        {
//            var apiKey = _configuration["GeminiAI:ApiKey"];
//            if (string.IsNullOrEmpty(apiKey))
//            {
//                throw new InvalidOperationException("Gemini API key is not configured. Please add 'GeminiAI:ApiKey' to appsettings.json");
//            }

//            var prompt = BuildPrompt(request);
//            var geminiRequest = new GeminiRequest
//            {
//                Contents = new List<GeminiContent>
//                {
//                    new GeminiContent
//                    {
//                        Parts = new List<GeminiPart>
//                        {
//                            new GeminiPart { Text = prompt }
//                        }
//                    }
//                },
//                GenerationConfig = new GeminiGenerationConfig
//                {
//                    Temperature = 0.7,
//                    MaxOutputTokens = 2048,
//                    ResponseMimeType = "application/json"
//                }
//            };

//            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
//            var httpClient = _httpClientFactory.CreateClient();
//            httpClient.Timeout = TimeSpan.FromSeconds(60);

//            var jsonOptions = new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
//            };

//            _logger.LogInformation("Sending request to Gemini AI for emergency plan generation. Alert: {AlertType}", request.AlertType);

//            var response = await httpClient.PostAsJsonAsync(endpoint, geminiRequest, jsonOptions);

//            if (!response.IsSuccessStatusCode)
//            {
//                var errorContent = await response.Content.ReadAsStringAsync();
//                _logger.LogError("Gemini API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
//                throw new Exception($"Gemini API error: {response.StatusCode} - {errorContent}");
//            }

//            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(jsonOptions);
            
//            if (geminiResponse?.Candidates == null || !geminiResponse.Candidates.Any())
//            {
//                throw new Exception("Gemini API returned no candidates");
//            }

//            var generatedText = geminiResponse.Candidates[0].Content.Parts[0].Text;
//            _logger.LogInformation("Gemini AI response received. Length: {Length} characters", generatedText?.Length ?? 0);

//            // Parse the JSON response from Gemini
//            var recommendation = JsonSerializer.Deserialize<EmergencyPlanRecommendation>(generatedText, jsonOptions);

//            if (recommendation == null)
//            {
//                throw new Exception("Failed to parse Gemini AI response");
//            }

//            _logger.LogInformation("Successfully generated emergency plan with {TaskCount} tasks", recommendation.Tasks.Count);
//            return recommendation;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error generating emergency plan with Gemini AI");
//            throw;
//        }
//    }

//    private string BuildPrompt(EmergencyPlanRequest request)
//    {
//        var sb = new StringBuilder();
//        sb.AppendLine("You are an agricultural expert specializing in rice cultivation emergencies.");
//        sb.AppendLine("Generate an emergency response plan in JSON format for the following situation:");
//        sb.AppendLine();
//        sb.AppendLine($"Alert Type: {request.AlertType}");
//        sb.AppendLine($"Title: {request.Title}");
//        sb.AppendLine($"Description: {request.Description}");
//        sb.AppendLine($"Severity: {request.Severity}");
//        sb.AppendLine($"Plot Area: {request.PlotArea} hectares");
        
//        if (!string.IsNullOrEmpty(request.RiceVariety))
//            sb.AppendLine($"Rice Variety: {request.RiceVariety}");
        
//        if (!string.IsNullOrEmpty(request.CurrentGrowthStage))
//            sb.AppendLine($"Current Growth Stage: {request.CurrentGrowthStage}");

//        if (request.DetectedPests != null && request.DetectedPests.Any())
//        {
//            sb.AppendLine($"AI-Detected Pests: {string.Join(", ", request.DetectedPests)}");
//            if (request.AiConfidence.HasValue)
//                sb.AppendLine($"Detection Confidence: {request.AiConfidence.Value:F2}%");
//        }

//        sb.AppendLine();
//        sb.AppendLine("Generate a JSON response with the following structure:");
//        sb.AppendLine(@"{
//  ""recommendedVersionName"": ""Version name suggestion (e.g., 'Emergency - Brown Planthopper - 2024-01-24')"",
//  ""resolutionReason"": ""Brief explanation of the emergency and response strategy"",
//  ""estimatedUrgencyHours"": 24,
//  ""tasks"": [
//    {
//      ""taskName"": ""Task name in Vietnamese"",
//      ""description"": ""Detailed description in Vietnamese"",
//      ""taskType"": ""PestControl, Fertilization, Irrigation, or Harvesting"",
//      ""executionOrder"": 1,
//      ""daysFromNow"": 0,
//      ""priority"": ""Critical, High, or Normal"",
//      ""materials"": [
//        {
//          ""materialName"": ""Material name (use common Vietnamese pesticide/fertilizer names)"",
//          ""quantityPerHa"": 1000,
//          ""unit"": ""ml, kg, gram, or liter"",
//          ""purpose"": ""Why this material is needed"",
//          ""alternatives"": [""Alternative material 1"", ""Alternative material 2""]
//        }
//      ],
//      ""rationale"": ""Why this task is necessary""
//    }
//  ],
//  ""generalAdvice"": ""Additional recommendations and precautions in Vietnamese"",
//  ""warnings"": [""Warning 1"", ""Warning 2""]
//}");

//        sb.AppendLine();
//        sb.AppendLine("Important guidelines:");
//        sb.AppendLine("- Provide 2-5 tasks depending on severity");
//        sb.AppendLine("- Use Vietnamese for task names, descriptions, and advice");
//        sb.AppendLine("- Suggest common Vietnamese pesticides and fertilizers");
//        sb.AppendLine("- Material quantities should be per hectare");
//        sb.AppendLine("- Tasks should be ordered chronologically");
//        sb.AppendLine("- Include monitoring and follow-up tasks");
//        sb.AppendLine("- For pest control: suggest immediate intervention and follow-up monitoring");
//        sb.AppendLine("- For weather damage: suggest recovery and protection measures");
//        sb.AppendLine("- For disease: suggest treatment and prevention measures");
//        sb.AppendLine();
//        sb.AppendLine("Return ONLY the JSON object, no additional text.");

//        return sb.ToString();
//    }
//}

//// Gemini API request/response models
//internal class GeminiRequest
//{
//    [JsonPropertyName("contents")]
//    public List<GeminiContent> Contents { get; set; } = new();

//    [JsonPropertyName("generationConfig")]
//    public GeminiGenerationConfig GenerationConfig { get; set; } = new();
//}

//internal class GeminiContent
//{
//    [JsonPropertyName("parts")]
//    public List<GeminiPart> Parts { get; set; } = new();
//}

//internal class GeminiPart
//{
//    [JsonPropertyName("text")]
//    public string Text { get; set; } = string.Empty;
//}

//internal class GeminiGenerationConfig
//{
//    [JsonPropertyName("temperature")]
//    public double Temperature { get; set; }

//    [JsonPropertyName("maxOutputTokens")]
//    public int MaxOutputTokens { get; set; }

//    [JsonPropertyName("responseMimeType")]
//    public string ResponseMimeType { get; set; } = string.Empty;
//}

//internal class GeminiResponse
//{
//    [JsonPropertyName("candidates")]
//    public List<GeminiCandidate> Candidates { get; set; } = new();
//}

//internal class GeminiCandidate
//{
//    [JsonPropertyName("content")]
//    public GeminiContent Content { get; set; } = new();
//}

