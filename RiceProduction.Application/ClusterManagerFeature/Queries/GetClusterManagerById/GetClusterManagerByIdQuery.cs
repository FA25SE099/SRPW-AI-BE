using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using System;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerById
{
    public class GetClusterManagerByIdQuery : IRequest<Result<ClusterManagerResponse>>
    {
        public Guid ClusterManagerId { get; set; }
    }
}
