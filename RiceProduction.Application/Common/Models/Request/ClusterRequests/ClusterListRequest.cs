using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Request.ClusterRequests
{
    public class ClusterListRequest
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
