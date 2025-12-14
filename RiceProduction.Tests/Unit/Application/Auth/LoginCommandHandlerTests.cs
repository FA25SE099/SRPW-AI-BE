using FluentAssertions;
using Moq;
using RiceProduction.Application.Auth.Commands.Login;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.Auth;

/// <summary>
/// Tests for LoginCommandHandler - validates user authentication
/// </summary>
public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityService> _mockIdentityService;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _mockIdentityService = new Mock<IIdentityService>();
        _handler = new LoginCommandHandler(_mockIdentityService.Object);
    }

    [Fact]
    public async Task Handle_NoEmailOrPhoneNumber_ReturnsFailure()
    {
        // Arrange
        var command = new LoginCommand
        {
            Email = null,
            PhoneNumber = null,
            Password = "Password123!",
            RememberMe = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Either email or phone number must be provided.");
    }

    [Fact]
    public async Task Handle_ValidEmailLogin_ReturnsSuccess()
    {
        // Arrange
        var userId = "user-123";
        var email = "farmer@example.com";
        var accessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";
        var refreshToken = "refresh-token-abc123";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var authResult = new AuthenticationResult
        {
            Succeeded = true,
            UserId = userId,
            UserName = "FarmerUser",
            Email = email,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            Roles = new List<string> { "Farmer" }
        };

        _mockIdentityService
            .Setup(s => s.LoginAsync(email, "Password123!", true))
            .ReturnsAsync(authResult);

        var command = new LoginCommand
        {
            Email = email,
            Password = "Password123!",
            RememberMe = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.AccessToken.Should().Be(accessToken);
        result.Data.RefreshToken.Should().Be(refreshToken);
        result.Data.ExpiresAt.Should().Be(expiresAt);
        result.Data.User.Should().NotBeNull();
        result.Data.User.Id.Should().Be(userId);
        result.Data.User.Email.Should().Be(email);
        result.Data.User.UserName.Should().Be("FarmerUser");
        result.Data.User.Role.Should().Be("Farmer");
        result.Message.Should().Be("Login successful");
    }

    [Fact]
    public async Task Handle_ValidPhoneNumberLogin_ReturnsSuccess()
    {
        // Arrange
        var userId = "user-456";
        var phoneNumber = "+84901234567";
        var accessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";
        var refreshToken = "refresh-token-xyz789";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var authResult = new AuthenticationResult
        {
            Succeeded = true,
            UserId = userId,
            UserName = "SupervisorUser",
            Email = "supervisor@example.com",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            Roles = new List<string> { "Supervisor" }
        };

        _mockIdentityService
            .Setup(s => s.LoginAsync(phoneNumber, "SecurePass456!", false))
            .ReturnsAsync(authResult);

        var command = new LoginCommand
        {
            PhoneNumber = phoneNumber,
            Password = "SecurePass456!",
            RememberMe = false
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.AccessToken.Should().Be(accessToken);
        result.Data.RefreshToken.Should().Be(refreshToken);
        result.Data.User.Role.Should().Be("Supervisor");
        _mockIdentityService.Verify(s => s.LoginAsync(phoneNumber, "SecurePass456!", false), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var email = "wrong@example.com";
        var authResult = new AuthenticationResult
        {
            Succeeded = false,
            Errors = new[] { "Invalid email or password." }
        };

        _mockIdentityService
            .Setup(s => s.LoginAsync(email, "WrongPassword", true))
            .ReturnsAsync(authResult);

        var command = new LoginCommand
        {
            Email = email,
            Password = "WrongPassword",
            RememberMe = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid email or password.");
        result.Message.Should().Be("Login failed");
    }

    [Fact]
    public async Task Handle_AccountLocked_ReturnsFailure()
    {
        // Arrange
        var email = "locked@example.com";
        var authResult = new AuthenticationResult
        {
            Succeeded = false,
            Errors = new[] { "Account is locked due to multiple failed login attempts." }
        };

        _mockIdentityService
            .Setup(s => s.LoginAsync(email, "Password123!", true))
            .ReturnsAsync(authResult);

        var command = new LoginCommand
        {
            Email = email,
            Password = "Password123!",
            RememberMe = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Account is locked due to multiple failed login attempts.");
    }

    [Fact]
    public async Task Handle_EmailPreferredOverPhoneNumber_UsesEmail()
    {
        // Arrange - Both email and phone number provided
        var email = "user@example.com";
        var phoneNumber = "+84901234567";
        
        var authResult = new AuthenticationResult
        {
            Succeeded = true,
            UserId = "user-789",
            UserName = "TestUser",
            Email = email,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Roles = new List<string> { "Farmer" }
        };

        _mockIdentityService
            .Setup(s => s.LoginAsync(email, "Password123!", true))
            .ReturnsAsync(authResult);

        var command = new LoginCommand
        {
            Email = email,
            PhoneNumber = phoneNumber, // Both provided
            Password = "Password123!",
            RememberMe = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        // Verify email was used (isEmail = true)
        _mockIdentityService.Verify(s => s.LoginAsync(email, "Password123!", true), Times.Once);
        // Verify phone number was NOT used
        _mockIdentityService.Verify(s => s.LoginAsync(phoneNumber, It.IsAny<string>(), false), Times.Never);
    }

    [Fact]
    public async Task Handle_MultipleRoles_ReturnsFirstRole()
    {
        // Arrange
        var authResult = new AuthenticationResult
        {
            Succeeded = true,
            UserId = "admin-123",
            UserName = "AdminUser",
            Email = "admin@example.com",
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Roles = new List<string> { "Admin", "Supervisor", "Farmer" } // Multiple roles
        };

        _mockIdentityService
            .Setup(s => s.LoginAsync("admin@example.com", "AdminPass!", true))
            .ReturnsAsync(authResult);

        var command = new LoginCommand
        {
            Email = "admin@example.com",
            Password = "AdminPass!",
            RememberMe = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.User.Role.Should().Be("Admin"); // Should be first role
    }

    [Fact]
    public void Constructor_InitializesWithIdentityService()
    {
        // Arrange & Act
        var handler = new LoginCommandHandler(_mockIdentityService.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
