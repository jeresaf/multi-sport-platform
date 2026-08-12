namespace SportsPlatform.Shared.Configuration;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string[] PreviousSecrets { get; set; } = Array.Empty<string>();
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
