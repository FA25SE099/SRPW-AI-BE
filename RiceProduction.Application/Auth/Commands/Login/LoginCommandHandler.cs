using MediatR;
using Microsoft.AspNetCore.Http.Features;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IIdentityService _identityService;

    public LoginCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Determine if using email or phone number
        string identifier;
        bool isEmail;
        
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            identifier = request.Email;
            isEmail = true;
        }
        else if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            identifier = request.PhoneNumber;
            isEmail = false;
        }
        else
        {
            return Result<LoginResponse>.Failure(new[] { "Either email or phone number must be provided." }, "Login failed");
        }
        
        var result = await _identityService.LoginAsync(identifier, request.Password, isEmail);

        if (!result.Succeeded)
        {
            return Result<LoginResponse>.Failure(result.Errors, "Login failed");
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
                Role = result.Roles.FirstOrDefault() ?? ""
            }
        };

        return Result<LoginResponse>.Success(loginResponse, "Login successful");
    }
}