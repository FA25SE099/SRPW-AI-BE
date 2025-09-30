using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.Auth.Commands.Logout;

public record LogoutCommand : IRequest<Result<LogoutResponse>>
{
    public Guid UserId { get; init; }
    public string? RefreshToken { get; init; }
}