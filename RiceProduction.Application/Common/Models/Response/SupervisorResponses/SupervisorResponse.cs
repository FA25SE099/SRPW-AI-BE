using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.SupervisorResponses
{
    public class SupervisorResponse
    {
        public Guid SupervisorId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public int CurrentFarmerCount { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }
}
