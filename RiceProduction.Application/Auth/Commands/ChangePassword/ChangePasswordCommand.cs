using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.Auth.Commands.ChangePassword;

public class ChangePasswordCommand : IRequest<Result>
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
