using FluentValidation;

namespace RiceProduction.Application.MaterialFeature.Commands.CreateMaterial
{
    public class CreateMaterialCommandValidator : AbstractValidator<CreateMaterialCommand>
    {
        public CreateMaterialCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name is required")
                .MaximumLength(255)
                .WithMessage("Name must not exceed 255 characters");

            RuleFor(x => x.Unit)
                .NotEmpty()
                .WithMessage("Unit is required")
                .MaximumLength(50)
                .WithMessage("Unit must not exceed 50 characters");

            RuleFor(x => x.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid MaterialType");

            RuleFor(x => x.Manufacturer)
                .MaximumLength(255)
                .When(x => !string.IsNullOrEmpty(x.Manufacturer))
                .WithMessage("Manufacturer must not exceed 255 characters");

            RuleFor(x => x.AmmountPerMaterial)
                .GreaterThan(0)
                .When(x => x.AmmountPerMaterial.HasValue)
                .WithMessage("Amount per material must be greater than 0");

            RuleFor(x => x.PricePerMaterial)
                .GreaterThan(0)
                .WithMessage("Price per material must be greater than 0 and is required");

            RuleFor(x => x.PriceValidFrom)
                .NotEmpty()
                .WithMessage("Price valid from date is required");
        }
    }
}

