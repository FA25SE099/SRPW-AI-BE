using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.ClusterManagerResponses
{
    public class ClusterManagerResponse
    {
        public Guid ClusterManagerId { get; set; }
        public string? ClusterManagerName { get; set; }
        public string? ClusterManagerPhoneNumber { get; set; }
        public string? Email { get; set; }
        public Guid? ClusterId { get; set; }
        public string? ClusterName { get; set; }

        public DateTime? AssignedDate { get; set; }
    }
}
