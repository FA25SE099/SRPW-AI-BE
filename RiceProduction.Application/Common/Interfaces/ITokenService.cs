using System.Security.Claims;

namespace RiceProduction.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string userName, string email, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    bool ValidateToken(string token);
    DateTime GetTokenExpiration(string token);
    Guid? GetUserIdFromToken(string token);
}