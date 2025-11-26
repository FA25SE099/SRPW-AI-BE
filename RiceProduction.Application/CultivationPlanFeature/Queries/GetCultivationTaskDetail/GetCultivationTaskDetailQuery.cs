using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using FluentValidation;
using System;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationTaskDetail;

public class GetCultivationTaskDetailQuery : IRequest<Result<CultivationTaskDetailResponse>>, ICacheable
{
    public Guid CultivationTaskId { get; set; }
    
    public string CacheKey => $"CultivationTaskDetail:{CultivationTaskId}";
}

public class GetCultivationTaskDetailQueryValidator : AbstractValidator<GetCultivationTaskDetailQuery>
{
    public GetCultivationTaskDetailQueryValidator()
    {
        RuleFor(x => x.CultivationTaskId).NotEmpty().WithMessage("Cultivation Task ID is required.");
    }
}