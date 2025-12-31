using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces.External;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RiceProduction.Infrastructure.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _apiKey = _configuration["GeminiApi:ApiKey"] ?? throw new InvalidOperationException("Gemini API Key không được cấu hình");
        _baseUrl = _configuration["GeminiApi:BaseUrl"] ?? "https://generativelanguage.googleapis.com";
        _model = _configuration["GeminiApi:Model"] ?? "gemini-1.5-flash";
    }

    public async Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
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
                }
            };

            var url = $"{_baseUrl}/v1beta/models/{_model}:generateContent?key={_apiKey}";
            
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Gemini API returned status code {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GeminiResponse>(jsonResponse);

            if (result?.Candidates == null || result.Candidates.Length == 0)
            {
                _logger.LogWarning("Gemini API returned empty response");
                return "Không thể tạo phản hồi từ AI";
            }

            return result.Candidates[0].Content.Parts[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            throw;
        }
    }

    public async Task<string> GenerateContentWithContextAsync(
        string prompt, 
        string context, 
        CancellationToken cancellationToken = default)
    {
        var fullPrompt = $"Ngữ cảnh: {context}\n\nCâu hỏi: {prompt}";
        return await GenerateContentAsync(fullPrompt, cancellationToken);
    }
}

// DTOs for Gemini API Response
public class GeminiResponse
{
    public Candidate[] Candidates { get; set; } = Array.Empty<Candidate>();
}

public class Candidate
{
    public Content Content { get; set; } = new();
}

public class Content
{
    public Part[] Parts { get; set; } = Array.Empty<Part>();
}

public class Part
{
    public string Text { get; set; } = string.Empty;
}
