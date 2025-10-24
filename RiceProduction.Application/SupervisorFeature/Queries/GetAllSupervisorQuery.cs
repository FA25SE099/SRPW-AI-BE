using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.SupervisorFeature.Queries
{
    public class GetAllSupervisorQuery : IRequest<PagedResult<List<SupervisorResponse>>>
    {
        public Guid ClusterId { get; set; }
        /// <summary>
        /// Filter by name or email
        /// </summary>
        public string? SearchNameOrEmail { get; set; }
        /// <summary>
        /// Filter by phone number
        /// </summary>
        public string? SearchPhoneNumber { get; set; }

        /// <summary>
        /// If value equal 0, get all without pagination
        /// </summary>
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
