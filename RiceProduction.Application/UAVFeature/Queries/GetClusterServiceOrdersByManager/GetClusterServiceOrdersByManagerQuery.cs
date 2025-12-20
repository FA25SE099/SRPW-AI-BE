using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.UAVFeature.Queries.GetClusterServiceOrdersByManager;

public class GetClusterServiceOrdersByManagerQuery : IRequest<PagedResult<List<UavServiceOrderResponse>>>
{
    public Guid ClusterManagerId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public List<RiceProduction.Domain.Enums.TaskStatus>? StatusFilter { get; set; }
}

public class GetClusterServiceOrdersByManagerQueryValidator : AbstractValidator<GetClusterServiceOrdersByManagerQuery>
{
    public GetClusterServiceOrdersByManagerQueryValidator()
    {
        RuleFor(x => x.ClusterManagerId).NotEmpty();
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
    }
}


