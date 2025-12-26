using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(
        UserManager<ApplicationUser> userManager,
        ILogger<ForgotPasswordCommandHandler> logger,
        IEmailService emailService)
    {
        _userManager = userManager;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Result.Failure("Email không được để trống");
            }
            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user == null)
            {
                _logger.LogWarning("Password reset requested for non-existent email: {Email}", request.Email);
                return Result.Failure("Email không tồn tại trong hệ thống");
            }
            if (!user.IsActive)
            {
                _logger.LogWarning("Password reset requested for inactive user: {UserId}", user.Id);
                return Result.Failure("Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên");
            }
            var newPassword = GenerateRandomPassword();
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to reset password for user {UserId}: {Errors}", user.Id, errors);
                return Result.Failure("Không thể đặt lại mật khẩu. Vui lòng thử lại sau");
            }
            var templateData = new
            {
                FullName = user.FullName ?? "User",
                Email = user.Email,
                TempPassword = newPassword
            };

            var emailResult = await _emailService.SendEmailWithTemplateAsync(
                to: user.Email!,
                subject: "Mật khẩu mới của bạn - Rice Production System",
                templateName: "password_reset_new",
                templateData: templateData,
                cancellationToken: cancellationToken);

            if (!emailResult.Succeeded)
            {
                _logger.LogError("Failed to send password reset email to {Email}: {Error}",
                    user.Email, emailResult.Message);

                return Result.Success(
                    "Mật khẩu đã được đặt lại nhưng không thể gửi email. " +
                    "Vui lòng liên hệ quản trị viên để lấy mật khẩu mới");
            }

            _logger.LogInformation(
                "Password reset successfully for user {UserId} ({Email}), new password sent to email",
                user.Id, user.Email);

            return Result.Success(
                "Mật khẩu mới đã được gửi đến email của bạn. " +
                "Vui lòng kiểm tra hộp thư và đổi mật khẩu sau khi đăng nhập");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for email: {Email}", request.Email);
            return Result.Failure("Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau");
        }
    }
    private string GenerateRandomPassword()
    {
        const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
        const string digitChars = "0123456789";
        const string specialChars = "!@#$%";

        var random = new Random();
        var password = new char[12]; 
        password[0] = upperChars[random.Next(upperChars.Length)];
        password[1] = lowerChars[random.Next(lowerChars.Length)];
        password[2] = digitChars[random.Next(digitChars.Length)];
        password[3] = specialChars[random.Next(specialChars.Length)];
        const string allChars = upperChars + lowerChars + digitChars + specialChars;
        for (int i = 4; i < password.Length; i++)
        {
            password[i] = allChars[random.Next(allChars.Length)];
        }
        return new string(password.OrderBy(x => random.Next()).ToArray());
    }
}