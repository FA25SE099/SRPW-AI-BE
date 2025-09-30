using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace RiceProduction.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    [MaxLength(255)]
    public string? FullName { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
    public string? RefreshToken { get; set; }

    public DateTime? LastActivityAt { get; set; }

  
    public bool IsVerified { get; set; } = false;
}
