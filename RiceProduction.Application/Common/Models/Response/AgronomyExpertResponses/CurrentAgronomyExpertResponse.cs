using System;

namespace RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses
{
    public class CurrentAgronomyExpertResponse
    {
        public string ExpertId { get; set; }
        public string? ExpertName { get; set; }
        public string? Email { get; set; }
        public string? ClusterId { get; set; }
        public string? ClusterName { get; set; }
        public string? AssignedDate { get; set; }
    }
}

