using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Application.ZaloFeature.Commands.RefreshToken;

public record RefreshZaloTokenCommand : IRequest<ZaloTokenResponse>
{
    public string RefreshToken { get; init; } = string.Empty;
}

public class RefreshZaloTokenCommandHandler : IRequestHandler<RefreshZaloTokenCommand, ZaloTokenResponse>
{
    private readonly IZaloOAuthService _oAuthService;

    public RefreshZaloTokenCommandHandler(IZaloOAuthService oAuthService)
    {
        _oAuthService = oAuthService;
    }

    public async Task<ZaloTokenResponse> Handle(RefreshZaloTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            throw new ArgumentException("Refresh token is required", nameof(request.RefreshToken));
        }

        var tokenResponse = await _oAuthService.RefreshAccessTokenAsync(
            request.RefreshToken, 
            cancellationToken);

        return tokenResponse;
    }
}
