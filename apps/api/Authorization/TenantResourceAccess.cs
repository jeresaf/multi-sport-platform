using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Registrations.Entities;

namespace SportsPlatform.Api.Authorization;

public static class TenantResourceAccess
{
    public static async Task<bool> CanAccessContestAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await database.Contests
            .Where(contest => contest.Id == contestId)
            .Select(contest => (Guid?)contest.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue && user.CanAccessTenant(tenantId.Value);
    }

    public static async Task<bool> CanAccessParticipantAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await database.Participants
            .Where(participant => participant.Id == participantId)
            .Select(participant => (Guid?)participant.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue && user.CanAccessTenant(tenantId.Value);
    }

    public static async Task<bool> CanAccessCompetitionAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid competitionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await database.Competitions
            .Where(competition => competition.Id == competitionId)
            .Select(competition => (Guid?)competition.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue && user.CanAccessTenant(tenantId.Value);
    }

    public static async Task<bool> CanAccessRegistrationAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        var resource = await database.Registrations
            .Where(registration => registration.Id == registrationId)
            .Select(registration => new
            {
                registration.ContestId,
                registration.CompetitionId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (resource is null) return false;

        Guid? tenantId = null;
        if (resource.CompetitionId.HasValue)
        {
            tenantId = await database.Competitions
                .Where(competition => competition.Id == resource.CompetitionId.Value)
                .Select(competition => (Guid?)competition.TenantId)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else if (resource.ContestId.HasValue)
        {
            tenantId = await database.Contests
                .Where(contest => contest.Id == resource.ContestId.Value)
                .Select(contest => (Guid?)contest.TenantId)
                .SingleOrDefaultAsync(cancellationToken);
        }
        return tenantId.HasValue && user.CanAccessTenant(tenantId.Value);
    }

    public static async Task<bool> CompetitionRegistrationResourcesShareTenantAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid competitionId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var competitionTenantId = await database.Competitions
            .Where(competition => competition.Id == competitionId)
            .Select(competition => (Guid?)competition.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        var participantTenantId = await database.Participants
            .Where(participant => participant.Id == participantId)
            .Select(participant => (Guid?)participant.TenantId)
            .SingleOrDefaultAsync(cancellationToken);

        return competitionTenantId.HasValue
            && participantTenantId == competitionTenantId
            && user.CanAccessTenant(competitionTenantId.Value);
    }

    public static async Task<bool> CanAccessRegistrationFormAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid formId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await database.Set<RegistrationForm>()
            .Where(form => form.Id == formId)
            .Select(form => (Guid?)form.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue && user.CanAccessTenant(tenantId.Value);
    }

    public static async Task<bool> CanAccessRegistrationSubmissionAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await (
                from submission in database.Set<RegistrationFormSubmission>()
                join form in database.Set<RegistrationForm>()
                    on submission.FormId equals form.Id
                where submission.Id == submissionId
                select (Guid?)form.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue && user.CanAccessTenant(tenantId.Value);
    }

    public static async Task<bool> RegistrationResourcesShareTenantAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid contestId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var contestTenantId = await database.Contests
            .Where(contest => contest.Id == contestId)
            .Select(contest => (Guid?)contest.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        var participantTenantId = await database.Participants
            .Where(participant => participant.Id == participantId)
            .Select(participant => (Guid?)participant.TenantId)
            .SingleOrDefaultAsync(cancellationToken);

        return contestTenantId.HasValue
            && participantTenantId == contestTenantId
            && user.CanAccessTenant(contestTenantId.Value);
    }

    public static async Task<bool> RegistrationFormResourcesShareTenantAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid tenantId,
        Guid? competitionId,
        CancellationToken cancellationToken = default)
    {
        if (!user.CanAccessTenant(tenantId)) return false;
        if (!competitionId.HasValue) return true;

        var competitionTenantId = await database.Competitions
            .Where(competition => competition.Id == competitionId.Value)
            .Select(competition => (Guid?)competition.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return competitionTenantId == tenantId;
    }

    public static async Task<bool> RegistrationSubmissionResourcesShareTenantAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid formId,
        Guid participantId,
        Guid? contestId,
        CancellationToken cancellationToken = default)
    {
        var formTenantId = await database.Set<RegistrationForm>()
            .Where(form => form.Id == formId)
            .Select(form => (Guid?)form.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        var participantTenantId = await database.Participants
            .Where(participant => participant.Id == participantId)
            .Select(participant => (Guid?)participant.TenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!formTenantId.HasValue
            || participantTenantId != formTenantId
            || !user.CanAccessTenant(formTenantId.Value))
            return false;

        if (!contestId.HasValue) return true;
        var contestTenantId = await database.Contests
            .Where(contest => contest.Id == contestId.Value)
            .Select(contest => (Guid?)contest.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return contestTenantId == formTenantId;
    }

    public static async Task<bool> CanAccessRoleAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await database.Roles
            .Where(role => role.Id == roleId)
            .Select(role => role.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue
            ? user.CanAccessTenant(tenantId.Value)
            : user.IsPlatformOperator();
    }

    public static async Task<bool> CanAccessUserAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await database.Users
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => candidate.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue
            ? user.CanAccessTenant(tenantId.Value)
            : user.IsPlatformOperator();
    }

    public static async Task<bool> CanAssignRoleAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var userTenantId = await database.Users
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => candidate.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        var roleTenantId = await database.Roles
            .Where(role => role.Id == roleId)
            .Select(role => role.TenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!userTenantId.HasValue || !user.CanAccessTenant(userTenantId.Value))
            return false;

        return roleTenantId == userTenantId
            || (!roleTenantId.HasValue && user.IsPlatformOperator());
    }

    public static async Task<bool> CanAccessTenantContactAsync(
        this ClaimsPrincipal user,
        SportsPlatformDbContext database,
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await database.TenantContacts
            .Where(contact => contact.Id == contactId)
            .Select(contact => (Guid?)contact.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return tenantId.HasValue && user.CanAccessTenant(tenantId.Value);
    }
}
