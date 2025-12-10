using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Services
{
    public class RicePestDetectionService : IRicePestDetectionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RicePestDetectionService> _logger;

        public RicePestDetectionService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<RicePestDetectionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<PestDetectionResult> DetectPestAsync(IFormFile file)
        {
            var aiApiUrl = _configuration["AiApi:BaseUrl"] ?? "https://2511db686886.ngrok-free.app";
            var endpoint = $"{aiApiUrl}/predict";

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(240);

            using var content = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream();
            
            // Ensure stream is at the beginning
            if (fileStream.CanSeek && fileStream.Position > 0)
            {
                fileStream.Position = 0;
            }
            
            using var streamContent = new StreamContent(fileStream);

            // Set content type only if it's not null or empty
            if (!string.IsNullOrEmpty(file.ContentType))
            {
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            }
            else
            {
                // Default to image/jpeg if ContentType is not provided
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            }
            
            content.Add(streamContent, "files", file.FileName);

            _logger.LogInformation("Sending request to AI API: {Endpoint} with file: {FileName}, Size: {FileSize} bytes", 
                endpoint, file.FileName, file.Length);

            // Create request message to add headers directly
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };
            
            // Add ngrok-skip-browser-warning header directly to the request
            // This ensures it's sent with the request
            request.Headers.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");
            request.Headers.TryAddWithoutValidation("User-Agent", "RiceProduction-API/1.0");

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("AI API returned error: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
                throw new Exception($"AI API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("AI API response received: {Response}", responseContent);

            AiApiRootResponse? rootResponse;
            try
            {
                rootResponse = System.Text.Json.JsonSerializer.Deserialize<AiApiRootResponse>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse AI API response. Raw response: {Response}", responseContent);
                throw new Exception($"Failed to parse AI API response: {ex.Message}");
            }

            if (rootResponse == null || !rootResponse.Results.Any())
            {
                _logger.LogError("AI API returned null or empty results. Raw response: {Response}", responseContent);
                throw new Exception("AI API returned null or empty results");
            }

            // Get the first result (since we're sending one image)
            var aiResponse = rootResponse.Results[0];

            _logger.LogInformation("AI API response parsed successfully. Filename: {Filename}, Detections: {Count}", 
                aiResponse.Filename, aiResponse.Detections.Count);

            return MapToResult(aiResponse);
        }



        private PestDetectionResult MapToResult(AiDetectionResponse aiResponse)
        {
            var result = new PestDetectionResult
            {
                HasPest = aiResponse.Detections.Any(),
                TotalDetections = aiResponse.Detections.Count,
                ImageInfo = new ImageInfo
                {
                    Width = aiResponse.ImageWidth,
                    Height = aiResponse.ImageHeight
                }
            };

            foreach (var detection in aiResponse.Detections)
            {
                result.DetectedPests.Add(new PestInfo
                {
                    PestName = FormatPestName(detection.ClassName),
                    Confidence = Math.Round(detection.Confidence * 100, 2),
                    ConfidenceLevel = GetConfidenceLevel(detection.Confidence),
                    Location = detection.Box
                });
            }

            return result;
        }

        private string FormatPestName(string className)
        {
            // Convert "rice_blast" to "Rice Blast"
            return string.Join(" ", className.Split('_')
                .Select(word => char.ToUpper(word[0]) + word.Substring(1)));
        }

        private string GetConfidenceLevel(double confidence)
        {
            return confidence switch
            {
                >= 0.9 => "Very High",
                >= 0.7 => "High",
                >= 0.5 => "Medium",
                _ => "Low"
            };
        }

    }
}
