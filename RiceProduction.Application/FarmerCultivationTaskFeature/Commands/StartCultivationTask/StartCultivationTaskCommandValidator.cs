using FluentValidation;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Commands.StartCultivationTask;

public class StartCultivationTaskCommandValidator : AbstractValidator<StartCultivationTaskCommand>
{
    public StartCultivationTaskCommandValidator()
    {
        RuleFor(x => x.CultivationTaskId)
            .NotEmpty()
            .WithMessage("Cultivation Task ID is required");

        RuleFor(x => x.WeatherConditions)
            .MaximumLength(500)
            .WithMessage("Weather conditions cannot exceed 500 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes cannot exceed 1000 characters");
    }
}

