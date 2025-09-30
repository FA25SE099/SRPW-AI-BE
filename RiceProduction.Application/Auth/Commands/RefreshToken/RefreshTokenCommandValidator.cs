using FluentValidation;

namespace RiceProduction.Application.Auth.Commands.RefreshToken;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(v => v.AccessToken)
            .NotEmpty()
            .WithMessage("Access token is required.");

        RuleFor(v => v.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required.");
    }
}