using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorList
{
    public class GetUavVendorQuery : IRequest<PagedResult<List<UavVendorResponse>>>
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; }
        public string? NameEmailSearch { get; set; }
        public string? GroupClusterSearch { get; set; }
        public string? PhoneNumber { get; set; }
    }
}