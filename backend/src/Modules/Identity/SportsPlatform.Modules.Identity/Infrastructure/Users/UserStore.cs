using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SportsPlatform.Modules.Identity.Infrastructure.Users;

/// <summary>
/// Identity's user store on <see cref="IdentityDbContext"/>, without roles. It never saves by itself: the DbContext
/// is the unit of work, so a handler's user changes, role assignments and audit entry commit in its one save.
/// </summary>
public sealed class UserStore : UserOnlyStore<User, IdentityDbContext, Guid>
{
    public UserStore(IdentityDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        AutoSaveChanges = false;
    }

    /// <summary>
    /// Marks the user changed, with a new concurrency stamp. Identity's own version attaches the user first, which sets a
    /// tracked, already changed user back to unchanged and takes its pending values as the originals: a second change
    /// before the save (a password reset that also deletes its token) would then check the wrong concurrency stamp and
    /// fail. Here a tracked user keeps the stamp it was loaded with until the save.
    /// </summary>
    public override Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);

        if (Context.Entry(user).State == EntityState.Detached)
        {
            Context.Attach(user);
        }

        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        Context.Update(user);
        return Task.FromResult(IdentityResult.Success);
    }
}
