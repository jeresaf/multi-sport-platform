using Microsoft.AspNetCore.Identity;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>Identity's hasher, counting every hash and verification: sign-in must do the same work on every path.</summary>
public sealed class CountingPasswordHasher : PasswordHasher<User>
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override string HashPassword(User user, string password)
    {
        Interlocked.Increment(ref _count);
        return base.HashPassword(user, password);
    }

    public override PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        Interlocked.Increment(ref _count);
        return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
