using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorProfile;

public class GetSupervisorProfileQuery : IRequest<Result<SupervisorProfileResponse>>
{
    public Guid SupervisorId { get; set; }
}
