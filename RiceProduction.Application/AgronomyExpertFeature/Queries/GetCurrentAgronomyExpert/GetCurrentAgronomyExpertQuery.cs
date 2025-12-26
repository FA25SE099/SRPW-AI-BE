using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetCurrentAgronomyExpert
{
    public class GetCurrentAgronomyExpertQuery : IRequest<Result<CurrentAgronomyExpertResponse>>
    {
    }
}

