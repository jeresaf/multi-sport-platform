using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets;

/// <summary>
/// <c>/api/admin/v1/ruleset-templates</c> (<c>ruleset.read</c>) and <c>/api/platform/v1/ruleset-templates</c>
/// (<c>platform.sport.read</c>): the same reads and DTOs in both (S05 D14, D16).
/// </summary>
internal static class RulesetTemplateEndpoints
{
    public const string Tag = "Ruleset templates";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder api) => api.MapGroup("/ruleset-templates").WithTags(Tag);
}
