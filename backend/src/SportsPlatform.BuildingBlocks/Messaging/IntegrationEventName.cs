using System.Globalization;
using System.Text.RegularExpressions;

namespace SportsPlatform.BuildingBlocks.Messaging;

/// <summary>An integration event's stable name and version, e.g. <c>ResultApproved</c> v1 for <c>ResultApproved.V1</c>.</summary>
public sealed partial record IntegrationEventName(string Name, int Version)
{
    public static IntegrationEventName Of(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType)
            || eventType.DeclaringType is not { IsClass: true, IsAbstract: true, IsSealed: true } declaringType
            || VersionName().Match(eventType.Name) is not { Success: true } version)
        {
            throw new ArgumentException(
                $"{eventType.FullName} must be an IIntegrationEvent named V<n> inside a static class named after the event (ResultApproved.V1).",
                nameof(eventType));
        }

        return new IntegrationEventName(declaringType.Name, int.Parse(version.Groups["version"].Value, CultureInfo.InvariantCulture));
    }

    public override string ToString() => $"{Name}.V{Version.ToString(CultureInfo.InvariantCulture)}";

    [GeneratedRegex(@"^V(?<version>[1-9][0-9]{0,3})\z")]
    private static partial Regex VersionName();
}
