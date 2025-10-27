using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Infrastructure.Implementation.Zalo;

public class ZaloZnsService : IZaloZnsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZaloZnsService> _logger;
    private const string ZnsApiUrl = "https://business.openapi.zalo.me/message/template";

    public ZaloZnsService(
        HttpClient httpClient,
        ILogger<ZaloZnsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ZnsResponse> SendZnsAsync(ZnsRequest request, string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                phone = request.Phone,
                template_id = request.TemplateId,
                template_data = request.TemplateData,
                sending_mode = request.SendingMode ?? "1",
                tracking_id = request.TrackingId
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, ZnsApiUrl)
            {
                Content = JsonContent.Create(requestBody, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            };

            httpRequest.Headers.Add("access_token", accessToken);

            _logger.LogInformation("Sending ZNS to phone: {Phone}, template: {TemplateId}, tracking: {TrackingId}",
                request.Phone, request.TemplateId, request.TrackingId);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("ZNS API Response: {Response}", responseContent);

            var znsResponse = JsonSerializer.Deserialize<ZnsResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (znsResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize ZNS response");
            }

            if (znsResponse.Error != 0)
            {
                _logger.LogError("ZNS API Error: {Error} - {Message}", znsResponse.Error, znsResponse.Message);
            }
            else
            {
                _logger.LogInformation("ZNS sent successfully. Message ID: {MsgId}", znsResponse.Data?.MsgId);
            }

            return znsResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ZNS to phone: {Phone}", request.Phone);
            throw;
        }
    }

    public async Task<BulkZnsSummary> SendBulkZnsAsync(
        List<BulkZnsRequest> requests,
        string accessToken,
        int maxConcurrency = 5,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        return await SendBulkZnsWithProgressAsync(
            requests,
            accessToken,
            null,
            maxConcurrency,
            maxRetries,
            cancellationToken);
    }

    public async Task<BulkZnsSummary> SendBulkZnsWithProgressAsync(
        List<BulkZnsRequest> requests,
        string accessToken,
        IProgress<BulkZnsProgress>? progress = null,
        int maxConcurrency = 5,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;
        var results = new ConcurrentBag<BulkZnsSendResult>();
        var processedCount = 0;
        var successCount = 0;
        var failedCount = 0;

        _logger.LogInformation("Starting bulk ZNS send: {TotalRequests} requests with max concurrency: {MaxConcurrency}",
            requests.Count, maxConcurrency);

        // Create retry policy with exponential backoff
        var retryPolicy = CreateRetryPolicy(maxRetries);

        // Create semaphore to limit concurrency
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = requests.Select(async bulkRequest =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await SendSingleZnsWithRetryAsync(
                    bulkRequest,
                    accessToken,
                    retryPolicy,
                    cancellationToken);

                results.Add(result);

                // Update counters
                Interlocked.Increment(ref processedCount);
                if (result.Success)
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failedCount);

                // Report progress
                progress?.Report(new BulkZnsProgress
                {
                    TotalRequests = requests.Count,
                    ProcessedRequests = processedCount,
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                    CurrentPhone = bulkRequest.Phone
                });

                _logger.LogInformation("Processed {Processed}/{Total} - Phone: {Phone}, Success: {Success}",
                    processedCount, requests.Count, bulkRequest.Phone, result.Success);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        stopwatch.Stop();
        var endTime = DateTime.UtcNow;

        var summary = new BulkZnsSummary
        {
            TotalRequests = requests.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            Results = results.OrderBy(r => r.TrackingId).ToList(),
            TotalDuration = stopwatch.Elapsed,
            StartTime = startTime,
            EndTime = endTime
        };

        _logger.LogInformation(
            "Bulk ZNS send completed: Total={Total}, Success={Success}, Failed={Failed}, Duration={Duration}s",
            summary.TotalRequests, summary.SuccessCount, summary.FailedCount, summary.TotalDuration.TotalSeconds);

        return summary;
    }

    private async Task<BulkZnsSendResult> SendSingleZnsWithRetryAsync(
        BulkZnsRequest bulkRequest,
        string accessToken,
        AsyncRetryPolicy retryPolicy,
        CancellationToken cancellationToken)
    {
        var result = new BulkZnsSendResult
        {
            TrackingId = bulkRequest.TrackingId,
            Phone = bulkRequest.Phone,
            Success = false
        };

        try
        {
            var znsRequest = new ZnsRequest
            {
                Phone = bulkRequest.Phone,
                TemplateId = bulkRequest.TemplateId,
                TemplateData = bulkRequest.TemplateData,
                TrackingId = bulkRequest.TrackingId
            };

            var response = await retryPolicy.ExecuteAsync(async () =>
            {
                result.RetryCount++;
                return await SendZnsAsync(znsRequest, accessToken, cancellationToken);
            });

            result.Response = response;
            result.Success = response.Error == 0;

            if (!result.Success)
            {
                result.ErrorMessage = response.Message;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to send ZNS to {Phone} after retries", bulkRequest.Phone);
        }

        return result;
    }

    private AsyncRetryPolicy CreateRetryPolicy(int maxRetries)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<InvalidOperationException>(ex =>
                ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} after {Delay}s due to: {Exception}",
                        retryCount, maxRetries, timeSpan.TotalSeconds, exception.Message);
                });
    }
}
