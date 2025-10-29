using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Commands.DeleteSeason
{
    public class DeleteSeasonCommand : IRequest<Result<Guid>>
    {
        public Guid SeasonId { get; set; }
    }
}

