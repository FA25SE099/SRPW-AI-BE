using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Application.Common.Interfaces.External;

public interface IZaloOAuthService
{
    string GenerateCodeVerifier();
    string GenerateCodeChallenge(string codeVerifier);
    string GenerateAuthorizationUrl(ZaloAuthorizationUrlRequest request);
    Task<ZaloTokenResponse> GetAccessTokenAsync(string authorizationCode, string codeVerifier, CancellationToken cancellationToken = default);
    Task<ZaloTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
