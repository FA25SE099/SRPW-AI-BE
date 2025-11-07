using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses
{
    public class AgronomyExpertResponse
    {
        public Guid ExpertId { get; set; }
        public string? ExpertName { get; set; }
        public string? ExpertPhoneNumber { get; set; }
        public string? Email { get; set; }
        public Guid? ClusterId { get; set; }
        public DateTime? AssignedDate { get; set; }
    }
}
