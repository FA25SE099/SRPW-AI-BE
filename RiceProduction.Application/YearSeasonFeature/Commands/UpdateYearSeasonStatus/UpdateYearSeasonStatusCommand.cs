using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeasonStatus;

public class UpdateYearSeasonStatusCommand : IRequest<Result<bool>>
{
    public Guid Id { get; set; }
    public SeasonStatus Status { get; set; }
}

