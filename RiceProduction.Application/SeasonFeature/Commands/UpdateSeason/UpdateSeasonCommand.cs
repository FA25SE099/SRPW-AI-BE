using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Commands.UpdateSeason
{
    public class UpdateSeasonCommand : IRequest<Result<Guid>>
    {
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? SeasonType { get; set; }
        public bool IsActive { get; set; }
    }
}

