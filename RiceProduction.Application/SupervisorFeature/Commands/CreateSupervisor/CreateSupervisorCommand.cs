using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.SupervisorFeature.Commands.CreateSupervisor
{
    public class CreateSupervisorCommand : IRequest<Result<Guid>>
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        /// <summary>
        /// Maximum number of farmers this supervisor can manage
        /// </summary>
        public int MaxFarmerCapacity { get; set; }
    }
}
