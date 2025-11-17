using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models.Response.AdminResponses
{
    public class UserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }
}
