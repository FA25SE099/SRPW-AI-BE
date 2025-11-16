using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterIdByManagerId
{
    public class GetClusterIdByManagerIdQuery : IRequest<Result<Guid?>>
    {
        public Guid ClusterManagerId { get; set; }
    }
}
