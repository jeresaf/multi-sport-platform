using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Modules.Identity.Services;

public interface ITokenService
{
    (string accessToken, DateTime expiresAt) GenerateAccessToken(User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions);
    string GenerateRefreshToken();
    string? ValidateRefreshToken(string refreshToken);
}