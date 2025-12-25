using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using FluentValidation;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetTodayTask;

public class GetTodayTasksQuery : IRequest<Result<List<TodayTaskResponse>>>, ICacheable
{
    public Guid FarmerId { get; set; }
    public Guid? PlotCultivationId { get; set; }

    public RiceProduction.Domain.Enums.TaskStatus? StatusFilter { get; set; }
    
    public string CacheKey => $"Tasks:Today:Farmer:{FarmerId}";
}

public class GetTodayTasksQueryValidator : AbstractValidator<GetTodayTasksQuery>
{
    public GetTodayTasksQueryValidator()
    {
        RuleFor(x => x.FarmerId).NotEmpty().WithMessage("Farmer ID is required.");
    }
}