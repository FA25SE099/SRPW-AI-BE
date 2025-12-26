using System.ComponentModel.DataAnnotations;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.ValidateCultivationPreferences;

public class ValidateCultivationPreferencesQuery : IRequest<Result<CultivationValidationDto>>
{
    [Required]
    public Guid PlotId { get; set; }
    
    [Required]
    public Guid YearSeasonId { get; set; }
    
    [Required]
    public Guid RiceVarietyId { get; set; }
    
    [Required]
    public DateTime PreferredPlantingDate { get; set; }
}

