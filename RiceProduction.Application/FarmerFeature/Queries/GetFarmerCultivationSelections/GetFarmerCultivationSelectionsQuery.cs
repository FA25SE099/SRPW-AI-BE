using System.ComponentModel.DataAnnotations;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmerCultivationSelections;

public class GetFarmerCultivationSelectionsQuery : IRequest<Result<FarmerCultivationSelectionsDto>>
{
    [Required]
    public Guid FarmerId { get; set; }
    
    [Required]
    public Guid YearSeasonId { get; set; }
}

