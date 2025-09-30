using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IIdentityService _identityService;

    public RefreshTokenCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var refreshRequest = new RefreshTokenRequest
        {
            AccessToken = request.AccessToken,
            RefreshToken = request.RefreshToken
        };

        var result = await _identityService.RefreshTokenAsync(refreshRequest);

        if (!result.Succeeded)
        {
            return Result<LoginResponse>.Failure(result.Errors, "Token refresh failed");
        }

        var loginResponse = new LoginResponse
        {
            AccessToken = result.AccessToken!,
            RefreshToken = result.RefreshToken!,
            ExpiresAt = result.ExpiresAt!.Value,
            User = new UserInfo
            {
                Id = result.UserId!,
                UserName = result.UserName!,
                Email = result.Email!,
                Roles = result.Roles.ToList()
            }
        };

        return Result<LoginResponse>.Success(loginResponse, "Token refreshed successfully");
    }
}