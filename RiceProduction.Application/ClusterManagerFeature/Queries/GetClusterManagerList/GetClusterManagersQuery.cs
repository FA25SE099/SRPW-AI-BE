using Microsoft.AspNetCore.Mvc.RazorPages;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList
{
    public class GetClusterManagersQuery : IRequest<PagedResult<List<ClusterManagerResponse>>>
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; }
        // True = Free, False = Assigned, Null = All
        public bool? FreeOrAssigned { get; set; }
        public string? Search { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
