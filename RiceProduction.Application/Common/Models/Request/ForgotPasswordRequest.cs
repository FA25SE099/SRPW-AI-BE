namespace RiceProduction.Application.Common.Models.Request;

public class ForgotPasswordRequest
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
