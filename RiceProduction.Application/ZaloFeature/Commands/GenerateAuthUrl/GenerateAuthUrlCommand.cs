using Microsoft.Extensions.Configuration;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Application.ZaloFeature.Commands.GenerateAuthUrl;

public record GenerateAuthUrlCommand : IRequest<GenerateAuthUrlResponse>
{
    public string RedirectUri { get; init; } = string.Empty;
    public string State { get; init; } = Guid.NewGuid().ToString();
}

public class GenerateAuthUrlResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class GenerateAuthUrlCommandHandler : IRequestHandler<GenerateAuthUrlCommand, GenerateAuthUrlResponse>
{
    private readonly IZaloOAuthService _oAuthService;
    private readonly IConfiguration _configuration;

    public GenerateAuthUrlCommandHandler(
        IZaloOAuthService oAuthService,
        IConfiguration configuration)
    {
        _oAuthService = oAuthService;
        _configuration = configuration;
    }

    public Task<GenerateAuthUrlResponse> Handle(GenerateAuthUrlCommand request, CancellationToken cancellationToken)
    {
        var codeVerifier = _oAuthService.GenerateCodeVerifier();
        var codeChallenge = _oAuthService.GenerateCodeChallenge(codeVerifier);

        var authUrlRequest = new ZaloAuthorizationUrlRequest
        {
            AppId = _configuration["Zalo:AppId"]!,
            RedirectUri = string.IsNullOrEmpty(request.RedirectUri) 
                ? _configuration["Zalo:RedirectUri"]! 
                : request.RedirectUri,
            CodeChallenge = codeChallenge,
            State = request.State
        };

        var authUrl = _oAuthService.GenerateAuthorizationUrl(authUrlRequest);

        var response = new GenerateAuthUrlResponse
        {
            AuthorizationUrl = authUrl,
            CodeVerifier = codeVerifier,
            State = request.State
        };

        return Task.FromResult(response);
    }
}
