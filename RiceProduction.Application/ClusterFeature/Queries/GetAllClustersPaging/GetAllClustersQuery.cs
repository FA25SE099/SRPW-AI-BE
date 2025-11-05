using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterResponses;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterFeature.Queries.GetAllClustersPaging
{
    public class GetAllClustersQuery : IRequest<PagedResult<List<ClusterResponse>>>
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; }
        // Search by cluster name
        public string? ClusterNameSearch { get; set; }
        // Search by cluster manager name or expert name
        public string? ManagerExpertNameSearch { get; set; }
        // Search by cluster manager PhoneNumber or expert PhoneNumber
        public string? PhoneNumber { get; set; }
        public SortBy SortBy { get; set; } = SortBy.NameAscending;
    }
}
