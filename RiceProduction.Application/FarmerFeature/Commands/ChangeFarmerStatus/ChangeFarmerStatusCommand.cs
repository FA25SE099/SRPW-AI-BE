using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FarmerFeature.Commands.ChangeFarmerStatus;

public class ChangeFarmerStatusCommand : IRequest<Result<Guid>>
{
    public Guid FarmerId { get; set; }
    public FarmerStatus NewStatus { get; set; }
    public string? Reason { get; set; }
}

public class ChangeFarmerStatusCommandValidator : AbstractValidator<ChangeFarmerStatusCommand>
{
    public ChangeFarmerStatusCommandValidator()
    {
        RuleFor(x => x.FarmerId)
            .NotEmpty()
            .WithMessage("Farmer ID is required.");

        RuleFor(x => x.NewStatus)
            .IsInEnum()
            .WithMessage("Invalid farmer status.");

        When(x => x.NewStatus == FarmerStatus.Warned || 
                  x.NewStatus == FarmerStatus.NotAllowed || 
                  x.NewStatus == FarmerStatus.Resigned, () =>
        {
            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Reason is required when changing status to Warned, NotAllowed, or Resigned.")
                .MaximumLength(500)
                .WithMessage("Reason cannot exceed 500 characters.");
        });
    }
}
