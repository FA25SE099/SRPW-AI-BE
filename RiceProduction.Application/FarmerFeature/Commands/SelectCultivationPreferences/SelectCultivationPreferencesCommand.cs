using System.ComponentModel.DataAnnotations;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Commands.SelectCultivationPreferences;

public class SelectCultivationPreferencesCommand : IRequest<Result<CultivationPreferenceDto>>
{
    [Required]
    public Guid PlotId { get; set; }
    
    [Required]
    public Guid YearSeasonId { get; set; }
    
    [Required]
    public Guid RiceVarietyId { get; set; }
    
    [Required]
    public DateTime PreferredPlantingDate { get; set; }
    
    public string? Notes { get; set; }
}

