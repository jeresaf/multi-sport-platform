using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>
/// The authorization matrix (S03-T5): every mapped admin and platform endpoint, read from the app's own endpoints, so a
/// new one is covered without editing this test. Each answers <c>403 AUTH_FORBIDDEN</c> to a signed-in user who holds
/// every role except those granting its action, and passes authorization for a user holding one that grants it. Only
/// the authorization step is checked: with no body and unknown IDs, the handler then fails on its own rules.
/// </summary>
public sealed class AuthorizationMatrixTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _adminPrefix = "/api/admin/";
    private const string _platformPrefix = "/api/platform/";

    [Fact]
    public async Task Every_admin_and_platform_endpoint_is_authorized_by_its_action()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var users = new MatrixUsers(App, kmsa);
        var endpoints = MatrixEndpoints();
        endpoints.ShouldNotBeEmpty();
        endpoints.ShouldContain(endpoint => endpoint.Path.StartsWith(_adminPrefix, StringComparison.Ordinal));
        endpoints.ShouldContain(endpoint => endpoint.Path.StartsWith(_platformPrefix, StringComparison.Ordinal));

        var failures = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var name = $"{endpoint.Method} {endpoint.Path} ({endpoint.Action.Code.Value})";
            if (endpoint.Unfilled is { } parameter)
            {
                failures.Add($"{name}: no test value for route parameter '{parameter}' (add one to {nameof(Fill)}).");
                continue;
            }

            if (await users.GrantingAsync(endpoint.Action, Ct) is not { } granted)
            {
                failures.Add($"{name}: no role grants the action.");
                continue;
            }

            var denied = await users.LackingAsync(endpoint.Action, Ct);
            var path = Fill(endpoint, kmsa);

            var (deniedStatus, deniedCode, deniedDecision) = await SendAsync(denied, endpoint, path);
            if (deniedStatus != HttpStatusCode.Forbidden || deniedCode != "AUTH_FORBIDDEN" || deniedDecision != false)
            {
                failures.Add($"{name}: without a granting role answered {(int)deniedStatus} {deniedCode} (evaluator: {Describe(deniedDecision)}).");
            }

            var (allowedStatus, allowedCode, allowedDecision) = await SendAsync(granted, endpoint, path);
            if (allowedDecision != true || allowedStatus == HttpStatusCode.Unauthorized)
            {
                failures.Add($"{name}: with a granting role answered {(int)allowedStatus} {allowedCode} (evaluator: {Describe(allowedDecision)}).");
            }
        }

        failures.ShouldBeEmpty();
    }

    /// <summary>A role-holding client, the response's status and <c>code</c>, and what the evaluator decided for the endpoint's action.</summary>
    private async Task<(HttpStatusCode Status, string? Code, bool? Decision)> SendAsync(HttpClient client, MatrixEndpoint endpoint, string path)
    {
        App.Authorization.Decisions.Clear();
        using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), path);
        using var response = await client.SendAsync(request, Ct);
        var decision = App.Authorization.Decisions
            .Where(d => d.Request.Action.Code.Value == endpoint.Action.Code.Value)
            .Select(d => (bool?)d.Allowed)
            .LastOrDefault();
        return (response.StatusCode, await CodeOfAsync(response), decision);
    }

    private static string Describe(bool? decision) => decision switch
    {
        true => "allowed",
        false => "denied",
        null => "not asked",
    };

    private async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/problem+json")
        {
            return null;
        }

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>The app's admin and platform endpoints that declare an action, one per method, in a stable order.</summary>
    private List<MatrixEndpoint> MatrixEndpoints() =>
    [
        .. App.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<RequiresActionMetadata>().Count == 1)
            .Select(endpoint => (Endpoint: endpoint, Path: Render(endpoint.RoutePattern, parameter => $"{{{parameter.Name}}}")))
            .Where(e => e.Path.StartsWith(_adminPrefix, StringComparison.Ordinal) || e.Path.StartsWith(_platformPrefix, StringComparison.Ordinal))
            .SelectMany(e => (e.Endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(method => new MatrixEndpoint(
                method,
                e.Path,
                e.Endpoint.RoutePattern,
                e.Endpoint.Metadata.GetRequiredMetadata<RequiresActionMetadata>().Action)))
            .OrderBy(endpoint => endpoint.Path, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal),
    ];

    /// <summary>
    /// The path with its route parameters filled: <c>tenantId</c> is the tenant (route-tenant endpoints refuse an
    /// unknown one before authorization), <c>module</c> a module, any other GUID a new one.
    /// </summary>
    private static string Fill(MatrixEndpoint endpoint, Guid tenantId) => Render(endpoint.Pattern, parameter =>
        ValueFor(parameter, tenantId) ?? throw new InvalidOperationException($"No value for '{parameter.Name}'."));

    /// <summary>The pattern as a path, each route parameter written by <paramref name="parameterValue"/>.</summary>
    private static string Render(RoutePattern pattern, Func<RoutePatternParameterPart, string> parameterValue) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => parameterValue(parameter),
            _ => throw new InvalidOperationException($"Unexpected route part {part.GetType().Name}."),
        }))));

    private static string? ValueFor(RoutePatternParameterPart parameter, Guid tenantId) => parameter.Name switch
    {
        "tenantId" => tenantId.ToString(),
        "module" => "identity",
        _ when parameter.ParameterPolicies.Any(policy => policy.Content == "guid") => Guid.CreateVersion7().ToString(),
        _ => null,
    };

    private sealed record MatrixEndpoint(string Method, string Path, RoutePattern Pattern, ActionDefinition Action)
    {
        /// <summary>The first route parameter the matrix has no value for, if any.</summary>
        public string? Unfilled => Pattern.Parameters.FirstOrDefault(parameter => ValueFor(parameter, Guid.Empty) is null)?.Name;
    }

    /// <summary>
    /// Users in the tenant (tenant actions) or on the platform (platform actions), made once per role set: one holding
    /// a role that grants the action, and one holding every other role of its scope (none when all of them grant it).
    /// </summary>
    private sealed class MatrixUsers(TestApp app, Guid tenantId)
    {
        // Fictional pilot-customer people (docs/product/pilot-customer.md style); a number is added after the last.
        private static readonly string[] _people =
        [
            "grace.nakato", "daniel.kato", "brian.ssemakula", "esther.nabirye", "ruth.akello", "isaac.mugisha",
            "sarah.namubiru", "patrick.opio", "joyce.achieng", "samuel.okello", "agnes.namutebi", "moses.wasswa",
        ];

        private readonly Dictionary<string, HttpClient> _clients = new(StringComparer.Ordinal);

        private static string MailboxFor(int index) =>
            index < _people.Length ? $"{_people[index]}@kmsa.example" : $"{_people[index % _people.Length]}.{(index / _people.Length) + 1}@kmsa.example";

        public async Task<HttpClient?> GrantingAsync(ActionDefinition action, CancellationToken cancellationToken)
        {
            var role = Candidates(action).FirstOrDefault(candidate => candidate.Grants(action));
            return role is null ? null : await ClientAsync(action.Scope, [role.Code], cancellationToken);
        }

        public Task<HttpClient> LackingAsync(ActionDefinition action, CancellationToken cancellationToken) =>
            ClientAsync(action.Scope, [.. Candidates(action).Where(role => !role.Grants(action)).Select(role => role.Code)], cancellationToken);

        /// <summary>The roles that could hold the action: tenant roles assignable tenant-wide, or the platform roles.</summary>
        private static IEnumerable<RoleDefinition> Candidates(ActionDefinition action) => action.Scope == ActionScope.Platform
            ? IdentityRoles.All.Where(role => role.Scope == RoleScope.Platform)
            : IdentityRoles.All.Where(role => role.Scope == RoleScope.Tenant && role.AssignableAt.Contains(AssignmentScope.Tenant));

        private async Task<HttpClient> ClientAsync(ActionScope scope, IReadOnlyList<string> roles, CancellationToken cancellationToken)
        {
            var key = $"{scope}:{string.Join(',', roles)}";
            if (_clients.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var mailbox = MailboxFor(_clients.Count);
            HttpClient client;
            if (scope == ActionScope.Platform)
            {
                roles.Count.ShouldBeLessThanOrEqualTo(1, "The matrix makes platform users with at most one role.");
                var userId = await AccountTestData.CreateUserAsync(
                    app.Services, mailbox, TestPasswords.New(), cancellationToken, platformRole: roles.FirstOrDefault());
                client = app.CreateClient().AsUser(userId);
            }
            else
            {
                var member = await CreateMemberAsync(app.Services, tenantId, mailbox, cancellationToken, [.. roles.Select(TenantWide)]);
                client = app.ClientFor(member.UserId, tenantId);
            }

            _clients[key] = client;
            return client;
        }
    }
}
