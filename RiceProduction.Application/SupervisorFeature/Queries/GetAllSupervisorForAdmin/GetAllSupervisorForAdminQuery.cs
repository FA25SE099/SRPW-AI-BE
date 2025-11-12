using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForAdmin
{
    public class GetAllSupervisorForAdminQuery : IRequest<PagedResult<List<SupervisorResponse>>>
    {
        /// <summary>
        /// Filter by name or email
        /// </summary>
        public string? SearchNameOrEmail { get; set; }
        /// <summary>
        /// Filter by phone number
        /// </summary>
        public string? SearchPhoneNumber { get; set; }
        public string? AdvancedSearch { get; set; }

        /// <summary>
        /// If value equal 0, get all without pagination
        /// </summary>
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
