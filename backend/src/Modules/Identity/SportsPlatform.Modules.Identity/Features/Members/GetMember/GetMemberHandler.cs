using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Members.GetMember;

/// <summary>One member of the request's tenant, removed ones included; another tenant's membership is not found (RLS).</summary>
public sealed class GetMemberHandler(IdentityDbContext context, AuditScope audit)
{
    public async Task<Result<MemberResponse>> HandleAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        audit.Describe(MemberEndpoints.EntityType, membershipId);
        var memberships = context.Set<TenantMembership>().AsNoTracking().Where(m => m.Id == membershipId);
        var member = await MemberResponse.Project(memberships, context.Set<User>()).SingleOrDefaultAsync(cancellationToken);
        return member is null ? IdentityErrors.MembershipNotFound : Result.Success(member);
    }
}
