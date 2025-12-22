using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommand : IRequest<Result>
{
    public string? Email { get; set; }

}

