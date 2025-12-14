using FluentValidation;

namespace RiceProduction.Application.Auth.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Either email or phone number must be provided
        RuleFor(v => v)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Either email or phone number must be provided.");

        // If email is provided, it must be valid
        When(v => !string.IsNullOrWhiteSpace(v.Email), () =>
        {
            RuleFor(v => v.Email)
                .EmailAddress()
                .WithMessage("Email must be a valid email address.");
        });

        // If phone number is provided, it should be validated (basic format check)
        When(v => !string.IsNullOrWhiteSpace(v.PhoneNumber), () =>
        {
            RuleFor(v => v.PhoneNumber)
                .Matches(@"^\+?[1-9]\d{1,14}$")
                .WithMessage("Phone number must be in a valid format (E.164 format recommended).");
        });

        RuleFor(v => v.Password)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(6)
            .WithMessage("Password must be at least 6 characters long.");
    }
}