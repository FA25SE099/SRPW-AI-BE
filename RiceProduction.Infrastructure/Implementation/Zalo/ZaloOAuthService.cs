using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Infrastructure.Implementation.Zalo;

public class ZaloOAuthService : IZaloOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZaloOAuthService> _logger;
    private readonly string _appId;
    private readonly string _secretKey;
    private const string OAuthTokenUrl = "https://oauth.zaloapp.com/v4/oa/access_token";
    private const string OAuthAuthorizeUrl = "https://oauth.zaloapp.com/v4/oa/permission";

    public ZaloOAuthService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ZaloOAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appId = configuration["Zalo:AppId"] ?? throw new InvalidOperationException("Zalo AppId is not configured");
        _secretKey = configuration["Zalo:SecretKey"] ?? throw new InvalidOperationException("Zalo SecretKey is not configured");
    }

    public string GenerateCodeVerifier()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var random = new Random();
        var codeVerifier = new string(Enumerable.Repeat(chars, 43)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        
        _logger.LogInformation("Generated code verifier");
        return codeVerifier;
    }

    public string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var asciiBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = sha256.ComputeHash(asciiBytes);
        var base64 = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        
        _logger.LogInformation("Generated code challenge from verifier");
        return base64;
    }

    public string GenerateAuthorizationUrl(ZaloAuthorizationUrlRequest request)
    {
        var queryParams = new Dictionary<string, string>
        {
            { "app_id", request.AppId },
            { "redirect_uri", request.RedirectUri },
            { "code_challenge", request.CodeChallenge },
            { "state", request.State }
        };

        var queryString = string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var authUrl = $"{OAuthAuthorizeUrl}?{queryString}";
        
        _logger.LogInformation("Generated authorization URL");
        return authUrl;
    }

    public async Task<ZaloTokenResponse> GetAccessTokenAsync(
        string authorizationCode, 
        string codeVerifier, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                { "code", authorizationCode },
                { "app_id", _appId },
                { "grant_type", "authorization_code" },
                { "code_verifier", codeVerifier }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            request.Headers.Add("secret_key", _secretKey);

            _logger.LogInformation("Requesting access token with authorization code");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Token response: {Response}", responseContent);

            response.EnsureSuccessStatusCode();

            var tokenResponse = JsonSerializer.Deserialize<ZaloTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize token response");
            }

            _logger.LogInformation("Successfully obtained access token");
            return tokenResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access token");
            throw;
        }
    }

    public async Task<ZaloTokenResponse> RefreshAccessTokenAsync(
        string refreshToken, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                { "refresh_token", refreshToken },
                { "app_id", _appId },
                { "grant_type", "refresh_token" }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            request.Headers.Add("secret_key", _secretKey);

            _logger.LogInformation("Refreshing access token");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Refresh token response: {Response}", responseContent);

            response.EnsureSuccessStatusCode();

            var tokenResponse = JsonSerializer.Deserialize<ZaloTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize token response");
            }

            _logger.LogInformation("Successfully refreshed access token");
            return tokenResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            throw;
        }
    }
}
