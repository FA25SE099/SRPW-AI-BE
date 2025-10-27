using System.ComponentModel.DataAnnotations;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Commands;

public class ChangeRiceSeasonCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid RiceId { get; set; }

    [Required]
    public Guid SeasonId { get; set; }

}
public class ChangeRiceSeasonCommandValidator : AbstractValidator<ChangeRiceSeasonCommand>
{
    public ChangeRiceSeasonCommandValidator()
    {
        
    }
    
}