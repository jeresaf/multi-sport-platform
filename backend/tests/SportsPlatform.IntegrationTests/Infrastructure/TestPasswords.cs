using System.Security.Cryptography;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>Passwords for tests: generated per test, never written down (S03 D16: no default credentials).</summary>
internal static class TestPasswords
{
    /// <summary>A random password the policy accepts (24 characters).</summary>
    public static string New() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

    /// <summary>A random password one character shorter than the policy's 12.</summary>
    public static string TooShort() => New()[..11];
}
