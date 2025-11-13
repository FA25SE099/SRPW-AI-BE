using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorById
{
    public class GetUavVendorByIdQuery : IRequest<Result<UavVendorDetailResponse>>
    {
        public Guid UavVendorId { get; set; }
    }
}
