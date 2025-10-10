using FluentValidation;

namespace RiceProduction.Application.ZaloFeature.Commands.SendBulkZns;

public class SendBulkZnsCommandValidator : AbstractValidator<SendBulkZnsCommand>
{
    public SendBulkZnsCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty()
            .WithMessage("Access token is required");

        RuleFor(x => x.Requests)
            .NotEmpty()
            .WithMessage("At least one request is required")
            .Must(x => x.Count <= 1000)
            .WithMessage("Maximum 1000 requests per bulk send");

        RuleFor(x => x.MaxConcurrency)
            .InclusiveBetween(1, 20)
            .WithMessage("Max concurrency must be between 1 and 20");

        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(0, 5)
            .WithMessage("Max retries must be between 0 and 5");

        RuleForEach(x => x.Requests).ChildRules(request =>
        {
            request.RuleFor(r => r.Phone)
                .NotEmpty()
                .Matches(@"^84\d{9,10}$")
                .WithMessage("Phone must be in format 84xxxxxxxxx");

            request.RuleFor(r => r.TemplateId)
                .NotEmpty()
                .WithMessage("Template ID is required");

            request.RuleFor(r => r.TrackingId)
                .NotEmpty()
                .MaximumLength(48)
                .WithMessage("Tracking ID is required and max 48 characters");
        });
    }
}
