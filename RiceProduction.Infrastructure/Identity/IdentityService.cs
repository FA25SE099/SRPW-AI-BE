using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Data;

namespace RiceProduction.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _userClaimsPrincipalFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _context;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserClaimsPrincipalFactory<ApplicationUser> userClaimsPrincipalFactory,
        IAuthorizationService authorizationService,
        ITokenService tokenService,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userClaimsPrincipalFactory = userClaimsPrincipalFactory;
        _authorizationService = authorizationService;
        _tokenService = tokenService;
        _context = context;
    }

    public async Task<string?> GetUserNameAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.UserName;
    }


    public async Task<UserDto> GetUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto
        {
            Id = user.Id.ToString(),
            UserName = user.FullName ?? "User don't have Fullname yet",
            Email = user.Email ?? "User don't have Email yet",
            Role = roles.FirstOrDefault() ?? string.Empty
        };
    }

    public async Task<(Result Result, Guid UserId)> CreateUserAsync(string userName, string password)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName,
        };

        var result = await _userManager.CreateAsync(user, password);

        return (result.ToApplicationResult(), user.Id);
    }

    public async Task<bool> IsInRoleAsync(Guid userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user != null && await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<bool> AuthorizeAsync(Guid userId, string policyName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            return false;
        }

        var principal = await _userClaimsPrincipalFactory.CreateAsync(user);

        var result = await _authorizationService.AuthorizeAsync(principal, policyName);

        return result.Succeeded;
    }

    public async Task<Result> DeleteUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        return user != null ? await DeleteUserAsync(user) : Result.Success();
    }

    public async Task<Result> DeleteUserAsync(ApplicationUser user)
    {
        var result = await _userManager.DeleteAsync(user);

        return result.ToApplicationResult();
    }

    public async Task<AuthenticationResult> LoginAsync(string emailOrPhone, string password, bool isEmail = true)
    {
        var user = isEmail
            ? await _userManager.FindByEmailAsync(emailOrPhone)
            : await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == emailOrPhone);

        if (user == null)
        {
            return FailedAuth(isEmail ? "Invalid email or password." : "Invalid phone number or password.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var error = result switch
            {
                { IsLockedOut: true } => "Account is locked out.",
                { IsNotAllowed: true } => "Account is not allowed to sign in.",
                _ => "Invalid email or password."
            };

            return FailedAuth(error);
        }

        return await GenerateTokensAndBuildResultAsync(user);
    }

    public async Task<Result> LogoutAsync(Guid userId, string? refreshToken = null)
    {
        try
        {
            await _signInManager.SignOutAsync();

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var tokenEntity = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId);

                if (tokenEntity != null)
                {
                    tokenEntity.IsRevoked = true;
                    tokenEntity.RevokedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId
                             && !rt.IsRevoked
                             && rt.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new[] { $"Logout failed: {ex.Message}" });
        }
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        var userIdString = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return FailedAuth("Invalid access token.");
        }

        var refreshTokenEntity = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId);

        if (refreshTokenEntity == null || !refreshTokenEntity.IsActive)
        {
            return FailedAuth("Invalid refresh token.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return FailedAuth("User not found.");
        }

        return await GenerateTokensAndBuildResultAsync(user, replacedToken: refreshTokenEntity);
    }

    public async Task<bool> IsTokenValidAsync(Guid userId, string refreshToken)
    {
        var tokenEntity = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId);

        return tokenEntity?.IsActive == true;
    }

    public async Task<Result> RevokeAllUserTokensAsync(Guid userId)
    {
        try
        {
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.IsActive)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new[] { $"Failed to revoke tokens: {ex.Message}" });
        }
    }

    private static AuthenticationResult FailedAuth(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };

    private async Task<AuthenticationResult> GenerateTokensAndBuildResultAsync(
        ApplicationUser user,
        RefreshToken? replacedToken = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.UserName!, user.Email!, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        if (replacedToken != null)
        {
            replacedToken.IsRevoked = true;
            replacedToken.RevokedAt = DateTime.UtcNow;
            replacedToken.ReplacedByToken = refreshToken;
        }

        _context.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _context.SaveChangesAsync();

        return new AuthenticationResult
        {
            Succeeded = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = _tokenService.GetTokenExpiration(accessToken),
            UserId = user.Id.ToString(),
            UserName = user.UserName,
            Email = user.Email,
            Roles = roles
        };
    }
}