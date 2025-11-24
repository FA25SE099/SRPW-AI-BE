using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetEmergencyPlan;

public class GetEmergencyPlansQuery : IRequest<PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Optional filter by Group ID
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Optional filter by Cluster ID
    /// </summary>
    public Guid? ClusterId { get; set; }

    /// <summary>
    /// Optional search term for plan name
    /// </summary>
    public string? SearchTerm { get; set; }
}
