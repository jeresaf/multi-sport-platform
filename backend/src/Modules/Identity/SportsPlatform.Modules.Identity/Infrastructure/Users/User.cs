using Microsoft.AspNetCore.Identity;

namespace SportsPlatform.Modules.Identity.Infrastructure.Users;

/// <summary>
/// A person who signs in: ASP.NET Core Identity's user, global (one user may belong to several tenants, S03 D8). Its
/// shape is Identity's, so it lives here rather than in <c>Domain/</c> (which may not depend on ASP.NET Core). The
/// email is the user name; both are <c>PRIVATE</c> (ADR-0015): never logged, audited or published.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
    // For EF Core and Identity's stores only.
    private User()
    {
    }

    private User(Guid id, string email)
    {
        Id = id;
        UserName = email;
        Email = email;
        SecurityStamp = Guid.NewGuid().ToString();
    }

    /// <summary>A new user with a UUIDv7 ID. <paramref name="emailConfirmed"/> only when the caller has proved it.</summary>
    public static User Create(string email, bool emailConfirmed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return new User(Guid.CreateVersion7(), email.Trim()) { EmailConfirmed = emailConfirmed };
    }
}
