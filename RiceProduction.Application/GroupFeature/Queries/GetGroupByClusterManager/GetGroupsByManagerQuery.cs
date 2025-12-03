using MediatR;
using RiceProduction.Application.Common.Models;
using FluentValidation;
using System;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.GroupFeature.Queries.GetGroupByClusterManager;

public class GetGroupsByManagerQuery : IRequest<PagedResult<List<ClusterManagerGroupResponse>>>
{
    public Guid ClusterManagerId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    
    public GroupStatus? StatusFilter { get; set; }
}

public class GetGroupsByManagerQueryValidator : AbstractValidator<GetGroupsByManagerQuery>
{
    public GetGroupsByManagerQueryValidator()
    {
        RuleFor(x => x.ClusterManagerId).NotEmpty().WithMessage("Cluster Manager ID is required.");
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
    }
}