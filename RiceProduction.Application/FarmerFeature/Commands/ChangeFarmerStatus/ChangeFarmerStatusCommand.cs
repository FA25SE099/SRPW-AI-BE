using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FarmerFeature.Commands.ChangeFarmerStatus;

public class ChangeFarmerStatusCommand : IRequest<Result<Guid>>
{
    public Guid FarmerId { get; set; }
    public FarmerStatus NewStatus { get; set; }
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
    }
}
