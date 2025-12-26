using FluentValidation;

namespace RiceProduction.Application.FarmerFeature.Commands.SelectCultivationPreferences;

public class SelectCultivationPreferencesCommandValidator : AbstractValidator<SelectCultivationPreferencesCommand>
{
    public SelectCultivationPreferencesCommandValidator()
    {
        RuleFor(x => x.PlotId)
            .NotEmpty()
            .WithMessage("Plot ID is required");

        RuleFor(x => x.YearSeasonId)
            .NotEmpty()
            .WithMessage("Year Season ID is required");

        RuleFor(x => x.RiceVarietyId)
            .NotEmpty()
            .WithMessage("Rice Variety ID is required");

        RuleFor(x => x.PreferredPlantingDate)
            .NotEmpty()
            .WithMessage("Preferred planting date is required");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters");
    }
}

