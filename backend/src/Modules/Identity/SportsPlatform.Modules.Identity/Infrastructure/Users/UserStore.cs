using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

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
}
