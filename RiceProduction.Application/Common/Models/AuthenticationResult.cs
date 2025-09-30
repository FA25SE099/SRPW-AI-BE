namespace RiceProduction.Application.Common.Models;

public class AuthenticationResult
{
    public bool Succeeded { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public IEnumerable<string> Errors { get; set; } = new List<string>();
}