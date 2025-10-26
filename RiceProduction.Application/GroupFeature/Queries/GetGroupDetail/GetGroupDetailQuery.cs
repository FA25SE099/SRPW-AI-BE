using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupResponse;
namespace RiceProduction.Application.GroupFeature.Queries.GetGroupDetail;

public class GetGroupDetailQuery : IRequest<Result<GroupDetailResponse>>
{
    public Guid GroupId { get; set; }
}

public class GetGroupDetailQueryValidator : AbstractValidator<GetGroupDetailQuery>
{
    public GetGroupDetailQueryValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Group ID is required.");
    }
}