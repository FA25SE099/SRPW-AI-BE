using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Commands.CreateYearSeason;

public class CreateYearSeasonCommand : IRequest<Result<Guid>>
{
    public Guid SeasonId { get; set; }
    public Guid ClusterId { get; set; }
    public int Year { get; set; }
    public Guid RiceVarietyId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? BreakStartDate { get; set; }
    public DateTime? BreakEndDate { get; set; }
    public DateTime? PlanningWindowStart { get; set; }
    public DateTime? PlanningWindowEnd { get; set; }
    public string? Notes { get; set; }
}

