using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetById
{
    public class GetFarmerByIdQueries : IRequest<FarmerDTO?>
    {
        public Guid Farmerid { get; set; }
        public GetFarmerByIdQueries(Guid id)
        {
            Farmerid = id; 
        }
    }
}
