using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupResponses;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.GroupFeature.Queries.GetGroupsByClusterId
{
    public class GetGroupsByClusterManagerIdQuery : IRequest<PagedResult<List<GroupResponse>>>
    {
        public Guid ClusterManagerUserId { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
