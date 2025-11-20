using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Application.ZaloFeature.Commands.ExchangeAuthCode;

public record ExchangeAuthCodeCommand : IRequest<ZaloTokenResponse>
{
    public string Code { get; init; } = string.Empty;
    public string CodeVerifier { get; init; } = string.Empty;
}

public class ExchangeAuthCodeCommandHandler : IRequestHandler<ExchangeAuthCodeCommand, ZaloTokenResponse>
{
    private readonly IZaloOAuthService _oAuthService;

    public ExchangeAuthCodeCommandHandler(IZaloOAuthService oAuthService)
    {
        _oAuthService = oAuthService;
    }

    public async Task<ZaloTokenResponse> Handle(ExchangeAuthCodeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            throw new ArgumentException("Authorization code is required", nameof(request.Code));
        }

        if (string.IsNullOrEmpty(request.CodeVerifier))
        {
            throw new ArgumentException("Code verifier is required", nameof(request.CodeVerifier));
        }

        var tokenResponse = await _oAuthService.GetAccessTokenAsync(
            request.Code, 
            request.CodeVerifier, 
            cancellationToken);

        return tokenResponse;
    }
}
