using System.ComponentModel.DataAnnotations;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Queries.GetAvailableRiceVarietiesForSeason;

public class GetAvailableRiceVarietiesForSeasonQuery : IRequest<Result<List<RiceVarietySeasonDto>>>
{
    [Required]
    public Guid SeasonId { get; set; }
    
    public bool OnlyRecommended { get; set; } = true;
}

