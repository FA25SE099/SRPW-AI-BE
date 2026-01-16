using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Commands.CreateYearSeason;

public class CreateYearSeasonCommand : IRequest<Result<Guid>>
{
    public Guid SeasonId { get; set; }
    public Guid ClusterId { get; set; }
    public int Year { get; set; }
    
    /// <summary>
    /// Optional: Rice variety ID. If not provided, AllowFarmerSelection should be true
    /// </summary>
    public Guid? RiceVarietyId { get; set; }
    
    /// <summary>
    /// Optional: Start date. If not provided, will use Season's default start date
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// Optional: End date. If not provided, will use Season's default end date
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    public DateTime? BreakStartDate { get; set; }
    public DateTime? BreakEndDate { get; set; }
    public DateTime? PlanningWindowStart { get; set; }
    public DateTime? PlanningWindowEnd { get; set; }
    
    /// <summary>
    /// Whether to allow farmers to select their own rice variety and planting date
    /// </summary>
    public bool AllowFarmerSelection { get; set; } = false;
    
    /// <summary>
    /// Start of farmer selection window
    /// </summary>
    public DateTime? FarmerSelectionWindowStart { get; set; }
    
    /// <summary>
    /// End of farmer selection window
    /// </summary>
    public DateTime? FarmerSelectionWindowEnd { get; set; }
    
    public string? Notes { get; set; }
}

