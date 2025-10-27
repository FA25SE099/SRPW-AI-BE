using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetDetailById
{
    public class GetFarmerDetailQueries : IRequest<FarmerDetailDTO>
    {
        public Guid FarmerId { get; set; }
        public GetFarmerDetailQueries(Guid farmerId )
        { 
            FarmerId = farmerId;
        }
    }
}
