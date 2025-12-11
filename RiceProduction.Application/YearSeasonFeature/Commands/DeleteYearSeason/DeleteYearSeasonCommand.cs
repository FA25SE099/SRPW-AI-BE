using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Commands.DeleteYearSeason;

public class DeleteYearSeasonCommand : IRequest<Result<bool>>
{
    public Guid Id { get; set; }
}

