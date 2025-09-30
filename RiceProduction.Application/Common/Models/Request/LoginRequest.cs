namespace RiceProduction.Application.Common.Models.Request;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool? RememberMe { get; set; } = true; // Default to true
}