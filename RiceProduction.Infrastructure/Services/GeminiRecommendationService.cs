using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Services;

public class GeminiRecommendationService : IGeminiRecommendationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiRecommendationService> _logger;
    private readonly ApplicationDbContext _context;

    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxOutputTokens;
    private readonly double _topP;
    private readonly int _topK;

    public GeminiRecommendationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiRecommendationService> logger,
        ApplicationDbContext context)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _context = context;

        // Load configuration with fallback to GeminiAI for team compatibility
        _apiKey = (_configuration["Gemini:ApiKey"] ?? _configuration["GeminiAI:ApiKey"] ?? throw new InvalidOperationException("Gemini API Key not configured")).Trim();
        _baseUrl = (_configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta").Trim();
        _model = (_configuration["Gemini:Model"] ?? "gemini-2.5-flash").Trim();
        _temperature = double.Parse(_configuration["Gemini:Temperature"] ?? "0.7");
        _maxOutputTokens = int.Parse(_configuration["Gemini:MaxOutputTokens"] ?? "2000");
        _topP = double.Parse(_configuration["Gemini:TopP"] ?? "0.95");
        _topK = int.Parse(_configuration["Gemini:TopK"] ?? "40");
        
        // Normalize BaseUrl
        if (_baseUrl.EndsWith("/"))
        {
            _baseUrl = _baseUrl.Substring(0, _baseUrl.Length - 1);
        }
    }

    public async Task<PestRecommendationResponse> GetRecommendationAsync(PestRecommendationRequest request)
    {
        try
        {
            _logger.LogInformation("Getting AI recommendation for {PestCount} detected pests", request.DetectedPests.Count);

            var response = new PestRecommendationResponse
            {
                Success = true,
                DetectedPestsSummary = request.DetectedPests.Select(p => new PestSummary
                {
                    PestName = p.PestName,
                    Confidence = p.Confidence,
                    DetectionCount = p.DetectionCount,
                    ConfidenceLevel = p.ConfidenceLevel ?? GetConfidenceLevel(p.Confidence)
                }).ToList()
            };

            // Step 1: Get protocols from database
            var protocols = await GetProtocolsFromDatabaseAsync(request.DetectedPests);
            response.RecommendedProtocols = protocols;

            // Step 2: Determine severity
            response.Severity = DetermineSeverity(request.DetectedPests);

            // Step 3: Get AI recommendation from Gemini
            var geminiRecommendation = await GetGeminiRecommendationAsync(request, protocols);
            response.AiRecommendation = geminiRecommendation;

            // Step 4: Extract cost and timeline from AI response (if available)
            response.EstimatedCost = ExtractCostEstimate(geminiRecommendation.TreatmentRecommendation);
            response.Timeline = ExtractTimeline(geminiRecommendation.TreatmentRecommendation);

            _logger.LogInformation("Successfully generated recommendation with {ProtocolCount} protocols", protocols.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pest recommendation");
            return new PestRecommendationResponse
            {
                Success = false,
                Warnings = new List<string> { $"Error: {ex.Message}" }
            };
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var testRequest = new PestRecommendationRequest
            {
                DetectedPests = new List<DetectedPestInput>
                {
                    new DetectedPestInput { PestName = "Test", Confidence = 50 }
                }
            };

            var prompt = "Say 'Hello' if you can read this message.";
            var geminiResponse = await CallGeminiApiAsync(prompt);

            return !string.IsNullOrEmpty(geminiResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API connection test failed");
            return false;
        }
    }

    #region Private Methods

    private async Task<List<RecommendedProtocol>> GetProtocolsFromDatabaseAsync(List<DetectedPestInput> detectedPests)
    {
        var pestNames = detectedPests.Select(p => p.PestName.ToLower()).ToList();

        var protocols = await _context.PestProtocols
            .Where(p => p.IsActive)
            .ToListAsync();

        var recommendedProtocols = new List<RecommendedProtocol>();
        int priority = 1;

        foreach (var pest in detectedPests.OrderByDescending(p => p.Confidence))
        {
            var matchingProtocols = protocols
                .Where(p => p.Name.ToLower().Contains(pest.PestName.ToLower()) ||
                           pest.PestName.ToLower().Contains(p.Name.ToLower()))
                .ToList();

            foreach (var protocol in matchingProtocols)
            {
                if (!recommendedProtocols.Any(rp => rp.ProtocolId == protocol.Id))
                {
                    recommendedProtocols.Add(new RecommendedProtocol
                    {
                        ProtocolId = protocol.Id,
                        Name = protocol.Name,
                        Description = protocol.Description,
                        Type = protocol.Type,
                        ImageLinks = protocol.ImageLinks,
                        Notes = protocol.Notes,
                        Priority = priority++,
                        MatchReason = $"Matches detected pest: {pest.PestName} (Confidence: {pest.Confidence:F2}%)"
                    });
                }
            }
        }

        _logger.LogInformation("Found {Count} matching protocols from database", recommendedProtocols.Count);
        return recommendedProtocols;
    }

    private async Task<GeminiRecommendation> GetGeminiRecommendationAsync(
        PestRecommendationRequest request,
        List<RecommendedProtocol> protocols)
    {
        var prompt = BuildPrompt(request, protocols);
        
        _logger.LogInformation("Calling Gemini API with prompt length: {Length} characters", prompt.Length);

        var geminiResponse = await CallGeminiApiAsync(prompt);

        return ParseGeminiResponse(geminiResponse);
    }

    private string BuildPrompt(PestRecommendationRequest request, List<RecommendedProtocol> protocols)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Bạn là một chuyên gia nông nghiệp chuyên về trồng lúa và phòng trừ sâu bệnh.");
        sb.AppendLine("Hãy phân tích kết quả phát hiện sâu bệnh và đưa ra khuyến nghị xử lý.");
        sb.AppendLine("YÊU CẦU QUAN TRỌNG: Không chào hỏi, không kết bài rườm rà. KHÔNG sử dụng dấu sao (**) để bôi đậm, KHÔNG sử dụng các thanh phân cách (===). Hãy trình bày văn bản thuần túy theo các mục sau:");
        sb.AppendLine();

        // Detected pests
        sb.AppendLine("DANH SÁCH SÂU BỆNH PHÁT HIỆN:");
        foreach (var pest in request.DetectedPests)
        {
            sb.AppendLine($"- {pest.PestName}: {pest.Confidence:F2}% ({pest.ConfidenceLevel}), Số lượng: {pest.DetectionCount}");
        }
        sb.AppendLine();

        if (request.FarmContext != null)
        {
            sb.AppendLine("THÔNG TIN RUỘNG LÚA:");
            if (!string.IsNullOrEmpty(request.FarmContext.Location)) sb.AppendLine($"- Vị trí: {request.FarmContext.Location}");
            if (request.FarmContext.FieldArea.HasValue) sb.AppendLine($"- Diện tích: {request.FarmContext.FieldArea} ha");
            if (!string.IsNullOrEmpty(request.FarmContext.Notes)) sb.AppendLine($"- Ghi chú: {request.FarmContext.Notes}");
            sb.AppendLine();
        }

        sb.AppendLine("CẤU TRÚC PHẢN HỒI (BẮT BUỘC):");
        sb.AppendLine("1. ĐÁNH GIÁ TÌNH HÌNH: (Phân tích mức độ nghiêm trọng)");
        sb.AppendLine("2. KHUYẾN NGHỊ XỬ LÝ: (Các bước cụ thể, thuốc khuyên dùng, liều lượng)");
        sb.AppendLine("3. BIỆN PHÁP PHÒNG NGỪA: (Cách tránh tái phát)");
        sb.AppendLine("4. LƯU Ý THÊM: (Các cảnh báo khác)");
        sb.AppendLine();
        sb.AppendLine("Hãy trả lời bằng tiếng Việt, thiết thực và chính xác.");

        return sb.ToString();
    }

    private async Task<string> CallGeminiApiAsync(string prompt)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(60);

        // Ensure model name doesn't already have 'models/' prefix to avoid double prefixing
        string modelPath = _model.StartsWith("models/") ? _model : $"models/{_model}";
        var endpoint = $"{_baseUrl}/{modelPath}:generateContent?key={_apiKey}";
        
        _logger.LogInformation("Attempting Gemini API call. BaseUrl: {BaseUrl}, ModelPath: {ModelPath}", _baseUrl, modelPath);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = _temperature,
                maxOutputTokens = _maxOutputTokens,
                topP = _topP,
                topK = _topK
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Sending request to Gemini API: {Endpoint}", endpoint.Replace(_apiKey, "***"));

        var response = await httpClient.PostAsync(endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new Exception($"Gemini API error: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Gemini API response received, length: {Length}", responseContent.Length);

        return responseContent;
    }

    private GeminiRecommendation ParseGeminiResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            var text = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            // Use regex to look for section headers more flexibly (handling Markdown ###, **, etc.)
            var assessment = ExtractSection(text, "1\\.\\s*ĐÁNH GIÁ TÌNH HÌNH", "2\\.\\s*KHUYẾN NGHỊ XỬ LÝ");
            var treatment = ExtractSection(text, "2\\.\\s*KHUYẾN NGHỊ XỬ LÝ", "3\\.\\s*BIỆN PHÁP PHÒNG NGỪA");
            var preventive = ExtractSection(text, "3\\.\\s*BIỆN PHÁP PHÒNG NGỪA", "4\\.\\s*LƯU Ý THÊM");
            var additional = ExtractSection(text, "4\\.\\s*LƯU Ý THÊM", null);

            return new GeminiRecommendation
            {
                Assessment = !string.IsNullOrEmpty(assessment) ? assessment : text,
                TreatmentRecommendation = treatment,
                PreventiveMeasures = preventive,
                AdditionalInsights = additional,
                RawResponse = text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Gemini response");
            return new GeminiRecommendation
            {
                Assessment = "Lỗi khi xử lý phản hồi từ AI",
                RawResponse = jsonResponse
            };
        }
    }

    private string ExtractSection(string text, string startHeaderPattern, string? nextHeaderPattern)
    {
        try 
        {
            // Find patterns like "1. ĐÁNH GIÁ...", "### 1. ĐÁNH GIÁ...", "**1. ĐÁNH GIÁ...**"
            var startMatch = System.Text.RegularExpressions.Regex.Match(text, startHeaderPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!startMatch.Success) return string.Empty;

            int startIndex = startMatch.Index + startMatch.Length;
            
            // Look for the next section to determine the end of current section
            int endIndex = text.Length;
            if (!string.IsNullOrEmpty(nextHeaderPattern))
            {
                var nextMatch = System.Text.RegularExpressions.Regex.Match(text, nextHeaderPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (nextMatch.Success)
                {
                    endIndex = nextMatch.Index;
                }
            }

            var content = text.Substring(startIndex, endIndex - startIndex).Trim();
            
            // Clean up leading/trailing Markdown or dots
            content = System.Text.RegularExpressions.Regex.Replace(content, @"^[:\*\s\-#]*", "");
            
            return content;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string DetermineSeverity(List<DetectedPestInput> pests)
    {
        var maxConfidence = pests.Max(p => p.Confidence);
        var totalDetections = pests.Sum(p => p.DetectionCount);

        if (maxConfidence >= 80 || totalDetections >= 5)
            return "High";
        if (maxConfidence >= 60 || totalDetections >= 3)
            return "Medium";
        return "Low";
    }

    private string GetConfidenceLevel(double confidence)
    {
        return confidence switch
        {
            >= 90 => "Very High",
            >= 70 => "High",
            >= 50 => "Medium",
            _ => "Low"
        };
    }

    private string? ExtractCostEstimate(string text)
    {
        // Simple extraction - can be improved with regex
        if (text.Contains("chi phí") || text.Contains("giá"))
        {
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (line.ToLower().Contains("chi phí") || line.ToLower().Contains("giá"))
                    return line.Trim();
            }
        }
        return null;
    }

    private string? ExtractTimeline(string text)
    {
        // Simple extraction
        if (text.Contains("thời gian") || text.Contains("ngay") || text.Contains("trong vòng"))
        {
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (line.ToLower().Contains("thời gian") || line.ToLower().Contains("ngay"))
                    return line.Trim();
            }
        }
        return null;
    }

    #endregion
}
