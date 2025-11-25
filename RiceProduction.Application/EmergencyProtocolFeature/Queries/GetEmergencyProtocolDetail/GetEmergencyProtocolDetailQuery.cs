using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.EmergencyProtocolResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Queries.GetEmergencyProtocolDetail;

public class GetEmergencyProtocolDetailQuery : IRequest<Result<EmergencyProtocolDetailDto>>
{
    public Guid EmergencyProtocolId { get; set; }
}
