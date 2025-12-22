using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        UserManager<ApplicationUser> userManager,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate passwords match
            if (request.NewPassword != request.ConfirmPassword)
            {
                return Result.Failure("New password and confirm password do not match");
            }

            // Validate email format
            if (string.IsNullOrEmpty(request.Email))
            {
                return Result.Failure("Email is required");
            }

            // Find the user by email
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Result.Failure("Invalid reset token or email");
            }

            // Check if user is active
            if (!user.IsActive)
            {
                return Result.Failure("User account is inactive");
            }

            // Reset the password using the token
            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Password reset failed for user {Email}: {Errors}", request.Email, errors);

                // Check for common errors and provide user-friendly messages
                if (errors.Contains("Invalid token") || errors.Contains("invalid"))
                {
                    return Result.Failure("Reset link has expired or is invalid. Please request a new password reset.");
                }

                return Result.Failure($"Failed to reset password: {errors}");
            }

            // Update security stamp to invalidate existing tokens
            await _userManager.UpdateSecurityStampAsync(user);

            _logger.LogInformation("Password reset successfully for user {UserId} with email {Email}", user.Id, user.Email);
            return Result.Success("Password has been reset successfully. You can now login with your new password.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for email {Email}", request.Email);
            return Result.Failure("An error occurred while resetting password");
        }
    }
}

