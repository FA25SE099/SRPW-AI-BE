using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorByClusterId
{
    public class GetSupervisorByClusterIdQuery : IRequest<List<SupervisorDTO>>
    {
        public Guid ClusterId { get; set; }
    }
}
