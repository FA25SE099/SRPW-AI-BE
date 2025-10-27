using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetApproved
{
    public class GetApprovedQueries : IRequest<Result<List<ProductionPlanDTO>>>
    {
        public Guid? GroupId { get; set; }
        public Guid? SupervisorId { get; set; }
        public DateTime? FromDate { get; set; } 
        public DateTime? ToDate { get; set; }
    }
}
