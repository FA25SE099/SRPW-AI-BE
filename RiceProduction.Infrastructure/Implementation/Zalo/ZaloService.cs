//using Azure.Core;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using RiceProduction.Application.Common.Interfaces;
//using RiceProduction.Application.Common.Models;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using ZaloCSharpSDK;
//using ZaloDotNetSDK;
//namespace RiceProduction.Infrastructure.Implementation.Zalo
//{
//    public class ZaloService : IZaloService
//    {
//        private readonly string _appId;
//        private readonly string _appSecret;
//       private readonly HttpClient _httpClient;
//        private readonly SemaphoreSlim _semaphore; // For bulk concurrency

//        public ZaloService(IConfiguration config, HttpClient httpClient)
//        {
//            _appId = config["Zalo:AppId"];
//            _appSecret = config["Zalo:AppSecret"];
//            _httpClient = httpClient;
//            _semaphore = new SemaphoreSlim(10, 10); // Max 10 concurrent sends; adjust based on quotas
//        }

//        public async Task<string> GetOAAccessTokenAsync(string code)
//        {
//            var url = "https://oauth.zaloapp.com/v4/oa/access_token";

//            var requestData = new
//            {
//                app_id = _appId,
//                app_secret = _appSecret,
//                code = code
//            };

//            var content = new StringContent(
//                JsonSerializer.Serialize(requestData),
//                Encoding.UTF8,
//                "application/json"
//            );

//            var response = await _httpClient.PostAsync(url, content);
//            var responseContent = await response.Content.ReadAsStringAsync();

//            if (response.IsSuccessStatusCode)
//            {
//                var tokenResponse = JsonSerializer.Deserialize<ZaloTokenResponse>(responseContent);
//                _accessToken = tokenResponse.access_token;
//                return _accessToken;
//            }

//            throw new Exception($"Failed to get access token: {responseContent}");
//        }

//        // Single ZNS Send (using SDK token)
//        public async Task<ZnsResult> SendZNSAsync(ZnsRequest request)
//        {
//            var token = await GetOAAccessTokenAsync();

//            var requestBody = new
//            {
//                phone = request.Phone,
//                template_id = request.TemplateId,
//                template_data = request.TemplateData, // Pass as object; matches your example
//                tracking_id =
//                    request.TrackingId ?? Guid.NewGuid().ToString("N").Substring(0, 32), // Auto-generate if null
//                sending_mode = request.SendingMode ?? "1"
//            };

//            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8,
//                "application/json");
//            _httpClient.DefaultRequestHeaders.Clear();
//            _httpClient.DefaultRequestHeaders.Add("access_token", token);

//            var response =
//                await _httpClient.PostAsync("https://business.openapi.zalo.me/message/template", jsonContent);
//            response.EnsureSuccessStatusCode();
//            var responseContent = await response.Content.ReadAsStringAsync();
//            var result = JObject.Parse(responseContent);

//            if ((int)result["error"] != 0)
//            {
//                return new ZnsResult
//                {
//                    Success = false,
//                    ErrorMessage = $"{result["message"]} (Code: {result["error"]})",
//                    Phone = request.Phone
//                };
//            }

//            var data = result["data"];
//            var quota = data["quota"];
//            return new ZnsResult
//            {
//                Success = true,
//                MessageId = data["msg_id"]?.ToString(),
//                SentTime = data["sent_time"]?.ToString(),
//                SendingMode = data["sending_mode"]?.ToString(),
//                RemainingQuota = quota?["remainingQuota"]?.ToString(),
//                DailyQuota = quota?["dailyQuota"]?.ToString(),
//                Phone = request.Phone,
//                TrackingId = request.TrackingId
//            };
//        }

//        // Bulk ZNS Send: For large user lists (e.g., 1000+)
//        public async Task<List<ZnsResult>> SendBulkZNSAsync(List<ZnsRequest> requests,
//            CancellationToken cancellationToken = default)
//        {
//            var results = new List<ZnsResult>();
//            var tasks = new List<Task<ZnsResult>>();

//            foreach (var request in requests)
//            {
//                // Validate phone format (E.164, VN example)
//                if (!IsValidZaloPhone(request.Phone))
//                {
//                    results.Add(new ZnsResult
//                        { Success = false, ErrorMessage = "Invalid phone format", Phone = request.Phone });
//                    continue;
//                }

//                // Quota check: Skip if exhausted (implement full monitoring as needed)
//                // For now, assume you check before bulk; add pause logic if remaining < batch size

//                tasks.Add(SendZNSWithSemaphoreAsync(request, cancellationToken));
//            }

//            // Execute in parallel with semaphore limit
//            var completedTasks = await Task.WhenAll(tasks);
//            results.AddRange(completedTasks);

//            // Summary stats
//            var successes = results.Count(r => r.Success);
//            var failures = results.Count(r => !r.Success);
//            Console.WriteLine($"Bulk Send Complete: {successes}/{requests.Count} succeeded, {failures} failed.");

//            return results;
//        }

//        public async Task<ZnsResult> SendZNSWithSemaphoreAsync(ZnsRequest request, CancellationToken ct)
//        {
//            await _semaphore.WaitAsync(ct);
//            try
//            {
//                return await SendZNSAsync(request);
//            }
//            finally
//            {
//                _semaphore.Release();
//            }
//        }

//        public bool IsValidZaloPhone(string phone)
//        {
//            return System.Text.RegularExpressions.Regex.IsMatch(phone, @"^(\+84|84|0)[1-9]\d{8,9}$") &&
//                phone.StartsWith("84") || phone.StartsWith("+84") || phone.StartsWith("0");
//        }

//        public void Dispose()
//        {
//            _semaphore?.Dispose();
//        }
//    }
//}