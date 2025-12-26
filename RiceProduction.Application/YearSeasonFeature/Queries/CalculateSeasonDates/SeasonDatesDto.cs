namespace RiceProduction.Application.YearSeasonFeature.Queries.CalculateSeasonDates;

/// <summary>
/// DTO containing calculated season dates for a specific year
/// </summary>
public class SeasonDatesDto
{
    /// <summary>
    /// Season ID
    /// </summary>
    public Guid SeasonId { get; set; }
    
    /// <summary>
    /// Season name (e.g., "Đông Xuân (Winter-Spring)")
    /// </summary>
    public string SeasonName { get; set; } = string.Empty;
    
    /// <summary>
    /// Year applied
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Original season start date in DD/MM format
    /// </summary>
    public string SeasonStartDateFormat { get; set; } = string.Empty;
    
    /// <summary>
    /// Original season end date in DD/MM format
    /// </summary>
    public string SeasonEndDateFormat { get; set; } = string.Empty;
    
    /// <summary>
    /// Calculated start date with year applied
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// Calculated end date with year applied (may be next year if season crosses years)
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// Whether this season crosses calendar years (e.g., Winter-Spring: Dec-Apr)
    /// </summary>
    public bool CrossesYears { get; set; }
    
    /// <summary>
    /// Duration in days
    /// </summary>
    public int DurationDays { get; set; }
    
    /// <summary>
    /// Suggested farmer selection window start (30 days before season)
    /// </summary>
    public DateTime? SuggestedFarmerSelectionWindowStart { get; set; }
    
    /// <summary>
    /// Suggested farmer selection window end (7 days before season start)
    /// </summary>
    public DateTime? SuggestedFarmerSelectionWindowEnd { get; set; }
    
    /// <summary>
    /// Suggested planning window start (for group formation + production plan creation)
    /// Starts after farmer selection ends
    /// </summary>
    public DateTime? SuggestedPlanningWindowStart { get; set; }
    
    /// <summary>
    /// Suggested planning window end (for group formation + production plan creation)
    /// Ends 3 days before season starts
    /// </summary>
    public DateTime? SuggestedPlanningWindowEnd { get; set; }
}

