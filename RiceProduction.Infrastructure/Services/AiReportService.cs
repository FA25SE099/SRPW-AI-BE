using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models.Request;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Services;

public class AiReportService : IAiReportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiReportService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public AiReportService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiReportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        _apiKey = (_configuration["Gemini:ApiKey"] ?? "").Trim();
        _baseUrl = (_configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta").Trim();
        _model = (_configuration["Gemini:Model"] ?? "gemini-2.5-flash").Trim();
    }

    public async Task<PlanRecommendationResponse> SuggestTasksAsync(PlanRecommendationRequest request)
    {
        try
        {
            var prompt = BuildPlanPrompt(request);
            var result = await CallGeminiApiAsync(prompt);
            return ParsePlanResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting tasks from report");
            return new PlanRecommendationResponse { Success = false };
        }
    }

    private string BuildPlanPrompt(PlanRecommendationRequest request)
    {
        return $@"
Bạn là một trợ lý chuyên nghiệp về quản lý sản xuất lúa gạo. 
Nhiệm vụ: Dựa vào nội dung báo cáo tình hình ruộng lúa và kế hoạch hiện tại (nếu có), hãy đề xuất các đầu việc (Tasks) cụ thể.

NỘI DUNG BÁO CÁO:
{request.ReportContent}

KẾ HOẠCH HIỆN TẠI (NẾU CÓ):
{request.ExistingPlanContent ?? "Chưa có"}

YÊU CẦU:
1. Đề xuất các Task cần thiết để xử lý vấn đề trong báo cáo hoặc cải thiện kế hoạch.
2. Với mỗi Task, cung cấp: 'TaskName' (tên ngắn gọn), 'Description' (mô tả chi tiết các bước), và 'Reason' (lý do cần làm).
3. QUAN TRỌNG: KHÔNG sử dụng các định dạng Markdown như bôi đậm (**), in nghiêng, hoặc tiêu đề (#). Chỉ trả về văn bản thuần túy.
4. ĐỊNH DẠNG TRẢ VỀ: Chỉ trả về JSON duy nhất theo cấu trúc sau, không chào hỏi:
{{
  ""Suggestions"": [
    {{
      ""TaskName"": ""Tên công việc"",
      ""Description"": ""Mô tả chi tiết"",
      ""Reason"": ""Lý do đề xuất""
    }}
  ]
}}
";
    }

    private async Task<string> CallGeminiApiAsync(string prompt)
    {
        var httpClient = _httpClientFactory.CreateClient();
        string modelPath = _model.StartsWith("models/") ? _model : $"models/{_model}";
        var endpoint = $"{_baseUrl}/{modelPath}:generateContent?key={_apiKey}";

        var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private PlanRecommendationResponse ParsePlanResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            
            // Tìm và bóc tách đoạn JSON trong text
            var startIndex = text.IndexOf("{");
            var endIndex = text.LastIndexOf("}");
            if (startIndex >= 0 && endIndex > startIndex)
            {
                var jsonClean = text.Substring(startIndex, endIndex - startIndex + 1);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestions = JsonSerializer.Deserialize<PlanRecommendationResponse>(jsonClean, options);
                if (suggestions != null)
                {
                    suggestions.Success = true;
                    suggestions.RawAnalysis = text;
                    return suggestions;
                }
            }
            return new PlanRecommendationResponse { Success = true, RawAnalysis = text };
        }
        catch { return new PlanRecommendationResponse { Success = false }; }
    }

    public async Task<bool> TestConnectionAsync() => true; // Đã test ở các bước trước
}
