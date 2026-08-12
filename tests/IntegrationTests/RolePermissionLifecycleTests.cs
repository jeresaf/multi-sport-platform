using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Controllers;
using SportsPlatform.Modules.Identity.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class RolePermissionLifecycleTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly ApiWebApplicationFactory _factory;

    public RolePermissionLifecycleTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthoritativeRoleCatalogue_IsCreatedForPlatformAndEveryTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var tenantIds = await database.Tenants.Select(tenant => tenant.Id).ToArrayAsync();
        var roles = await database.Roles
            .Include(role => role.Permissions)
            .ToArrayAsync();

        roles.Where(role => role.TenantId == null && role.IsSystemRole)
            .Select(role => role.Name)
            .Should().Contain(SystemRoleCatalogue.PlatformRoles.Select(role => role.Name));
        foreach (var tenantId in tenantIds)
        {
            var tenantRoles = roles.Where(role => role.TenantId == tenantId).ToArray();
            tenantRoles.Select(role => role.Name)
                .Should().Contain(SystemRoleCatalogue.TenantRoles.Select(role => role.Name));
            tenantRoles.Where(role => SystemRoleCatalogue.TenantRoles.Any(definition =>
                    definition.Name == role.Name))
                .Should().OnlyContain(role => role.IsSystemRole);
            tenantRoles.Where(role => SystemRoleCatalogue.TenantRoles.Any(definition =>
                    definition.Name == role.Name))
                .Should().OnlyContain(role => role.Permissions.All(permission =>
                    SystemPermissions.IsTenantAssignable(permission.Permission)));
        }

        SystemRoleCatalogue.Definitions.Select(role => role.Name)
            .Should().BeEquivalentTo(new[]
            {
                "Platform Owner", "Platform Support", "Tenant Owner",
                "Competition Administrator", "Registration Officer", "Fixture Manager",
                "Head Official", "Scorer", "Result Approver", "Media Manager", "Viewer"
            });
        SystemPermissions.All.Should().OnlyContain(permission =>
            permission == permission.Trim().ToLowerInvariant() && permission.Count(character => character == '.') == 1);

        var declaredApiPermissions = typeof(RolesController).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes(typeof(RequirePermissionAttribute), true)
                .Cast<RequirePermissionAttribute>()
                .Concat(type.GetMethods().SelectMany(method =>
                    method.GetCustomAttributes(typeof(RequirePermissionAttribute), true)
                        .Cast<RequirePermissionAttribute>())))
            .Select(attribute => attribute.Permission)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        declaredApiPermissions.Should().OnlyContain(permission =>
            SystemPermissions.All.Contains(permission),
            "every API authorization capability must come from the canonical catalogue");
    }

    [Fact]
    public async Task TenantOwner_CanAssignCanonicalCustomRole_WhilePlatformCapabilitiesAreRejected()
    {
        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var admin = await LoginAsync(client, "admin@premier.com", "TenantAdmin123!", $"role-admin-{suffix}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var rolesResponse = await client.GetAsync("/api/roles");
        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenantRoles = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
        tenantRoles.EnumerateArray().Select(role => role.GetProperty("name").GetString())
            .Should().Contain(SystemRoleCatalogue.TenantRoles.Select(role => role.Name));

        var catalogue = await client.GetFromJsonAsync<string[]>("/api/roles/permissions");
        catalogue.Should().Contain("users.read");
        catalogue.Should().NotContain("support.manage");

        var forbiddenRole = await client.PostAsJsonAsync("/api/roles", new
        {
            name = $"Forbidden {suffix}",
            permissions = new[] { "support.manage" }
        });
        forbiddenRole.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var unknownRole = await client.PostAsJsonAsync("/api/roles", new
        {
            name = $"Unknown {suffix}",
            permissions = new[] { "legacy.users.view" }
        });
        unknownRole.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var createRole = await client.PostAsJsonAsync("/api/roles", new
        {
            name = $"Roster reader {suffix}",
            description = "Focused authorization acceptance role",
            permissions = new[] { "users.read" }
        });
        createRole.StatusCode.Should().Be(HttpStatusCode.Created, await createRole.Content.ReadAsStringAsync());
        var createdRole = await createRole.Content.ReadFromJsonAsync<JsonElement>();
        var roleId = createdRole.GetProperty("id").GetGuid();
        createdRole.GetProperty("tenantId").GetGuid().Should().Be(PremierTenantId);

        var email = $"role-user-{suffix}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "RoleUser123!",
            firstName = "Role",
            lastName = "User",
            tenantId = PremierTenantId,
            deviceId = $"role-user-register-{suffix}"
        });
        registration.StatusCode.Should().Be(HttpStatusCode.OK, await registration.Content.ReadAsStringAsync());
        var registered = await registration.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registered.GetProperty("user").GetProperty("id").GetGuid();

        var assignment = await client.PostAsJsonAsync("/api/roles/assign", new { userId, roleId });
        assignment.StatusCode.Should().Be(HttpStatusCode.OK, await assignment.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Authorization = null;
        var user = await LoginAsync(client, email, "RoleUser123!", $"role-user-login-{suffix}");
        user.User.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().Contain($"Roster reader {suffix}");
        user.User.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString())
            .Should().Contain("users.read");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        (await client.GetAsync("/api/auth/users")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/auth/users/{admin.User.GetProperty("id").GetGuid()}/suspend",
            new { reason = "Must be denied" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        (await client.DeleteAsync($"/api/roles/{roleId}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/roles/remove", new { userId, roleId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/roles/{roleId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var auditScope = _factory.Services.CreateScope();
        var auditDatabase = auditScope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await auditDatabase.AuditEntries
            .Where(entry => entry.EntityId == roleId.ToString())
            .Select(entry => entry.Action)
            .ToArrayAsync())
            .Should().Contain(new[]
            {
                "PermissionRoleCreated", "PermissionRoleAssigned",
                "PermissionRoleRemoved", "PermissionRoleDeleted"
            });
    }

    private static async Task<AuthEnvelope> LoginAsync(
        HttpClient client,
        string email,
        string password,
        string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Role permission test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new AuthEnvelope(
            json.GetProperty("accessToken").GetString()!,
            json.GetProperty("user"));
    }

    private sealed record AuthEnvelope(string AccessToken, JsonElement User);
}
