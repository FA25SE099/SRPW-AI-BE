using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;

namespace RiceProduction.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(Guid userId);
    Task<bool> IsInRoleAsync(Guid userId, string role);
    Task<bool> AuthorizeAsync(Guid userId, string policyName);
    Task<(Result Result, Guid UserId)> CreateUserAsync(string userName, string password);
    Task<Result> DeleteUserAsync(Guid userId);
    Task<AuthenticationResult> LoginAsync(string email, string password);
    Task<Result> LogoutAsync(Guid userId, string? refreshToken = null);
    Task<AuthenticationResult> RefreshTokenAsync(RefreshTokenRequest request);
    Task<bool> IsTokenValidAsync(Guid userId, string refreshToken);
    Task<Result> RevokeAllUserTokensAsync(Guid userId);
}
