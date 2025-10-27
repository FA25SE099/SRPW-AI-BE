using FluentValidation;

namespace RiceProduction.Application.SmsFeature.Commands.TestSendSms;

public class TestSendSmsCommandValidator : AbstractValidator<TestSendSmsCommand>
{
    public TestSendSmsCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format. Use E.164 format (e.g., +1234567890).");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(1000).WithMessage("Message cannot exceed 1000 characters.");

        RuleFor(x => x.RecipientId)
            .NotEmpty().WithMessage("Recipient ID is required.");
    }
}
