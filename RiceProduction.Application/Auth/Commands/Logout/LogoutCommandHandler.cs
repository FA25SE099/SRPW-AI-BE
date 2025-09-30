using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.Auth.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<LogoutResponse>>
{
    private readonly IIdentityService _identityService;

    public LogoutCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result<LogoutResponse>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.LogoutAsync(request.UserId, request.RefreshToken);

        if (!result.Succeeded)
        {
            return Result<LogoutResponse>.Failure(result.Errors, "Logout failed");
        }

        var logoutResponse = new LogoutResponse();
        return Result<LogoutResponse>.Success(logoutResponse, "Logout successful");
    }
}