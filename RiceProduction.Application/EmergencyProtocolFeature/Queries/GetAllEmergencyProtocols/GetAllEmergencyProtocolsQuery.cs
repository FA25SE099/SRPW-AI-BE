using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.EmergencyProtocolResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Queries.GetAllEmergencyProtocols;

public class GetAllEmergencyProtocolsQuery : IRequest<PagedResult<List<EmergencyProtocolDto>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public Guid? CategoryId { get; set; }
    public string? SearchTerm { get; set; }
    public bool? IsActive { get; set; }
}