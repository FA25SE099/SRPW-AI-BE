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
        var userDto = new UserDto
        {
            Id = user.Id.ToString(),
            UserName = user.FullName ?? "User don't have Fullname yet",
            Email = user.Email ?? "User don't have Email yet",
            Role = roles.FirstOrDefault() ?? string.Empty
        };
        return userDto;
    }


    //public async Task<IList<string>> GetUserRolesAsync(Guid userId)
    //{
    //    var user = await _userManager.FindByIdAsync(userId.ToString());
    //    if (user == null)
    //    {
    //        return new List<string>();
    //    }

    //    return await _userManager.GetRolesAsync(user);
    //}
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

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return new AuthenticationResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid email or password." }
            };
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var errors = new List<string>();
            if (result.IsLockedOut)
                errors.Add("Account is locked out.");
            else if (result.IsNotAllowed)
                errors.Add("Account is not allowed to sign in.");
            else
                errors.Add("Invalid email or password.");

            return new AuthenticationResult
            {
                Succeeded = false,
                Errors = errors
            };
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.UserName!, user.Email!, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Store refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // Configure this
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
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

            // Revoke all refresh tokens for the user
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
            return Result.Failure(new[] { $"Logout failed: {ex.Message}" });
        }
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            return new AuthenticationResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid access token." }
            };
        }

        var userIdString = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return new AuthenticationResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid access token." }
            };
        }

        var refreshTokenEntity = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId);

        if (refreshTokenEntity == null || !refreshTokenEntity.IsActive)
        {
            return new AuthenticationResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid refresh token." }
            };
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new AuthenticationResult
            {
                Succeeded = false,
                Errors = new[] { "User not found." }
            };
        }

        // Revoke the current refresh token
        refreshTokenEntity.IsRevoked = true;
        refreshTokenEntity.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.GenerateAccessToken(user.Id, user.UserName!, user.Email!, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        // Store new refresh token
        var newRefreshTokenEntity = new RefreshToken
        {
            Token = newRefreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        refreshTokenEntity.ReplacedByToken = newRefreshToken;
        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        return new AuthenticationResult
        {
            Succeeded = true,
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = _tokenService.GetTokenExpiration(newAccessToken),
            UserId = user.Id.ToString(),
            UserName = user.UserName,
            Email = user.Email,
            Roles = roles
        };
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
}