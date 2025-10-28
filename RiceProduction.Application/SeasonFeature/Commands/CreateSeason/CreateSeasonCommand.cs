using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Commands.CreateSeason
{
    public class CreateSeasonCommand : IRequest<Result<Guid>>
    {
        public string SeasonName { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? SeasonType { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

