using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertById
{
    public class GetAgronomyExpertByIdQuery : IRequest<Result<AgronomyExpertResponse>>
    {
        public Guid AgronomyExpertId { get; set; }
    }
}
