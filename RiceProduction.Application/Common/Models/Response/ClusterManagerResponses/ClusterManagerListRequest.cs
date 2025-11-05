using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.ClusterManagerResponses
{
    public class ClusterManagerListRequest
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; }
        // True = Free, False = Assigned, Null = All
        public bool? FreeOrAssigned { get; set; }
        public string? Search { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
